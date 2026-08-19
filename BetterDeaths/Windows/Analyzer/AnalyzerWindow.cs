namespace BetterDeaths.Windows.Analyzer;

using BetterDeaths.Analysis.Sessions;
using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using BetterDeaths.Windows.Analyzer.Panels;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

internal sealed class AnalyzerWindow : Window, IAnalyzerWorkspaceNavigation, IAnalyzerSessionNavigation, IDisposable
{
    private const int PullQueryLimit = 100;
    private const int SessionQueryLimit = 500;
    private const float PullBrowserMinWidth = 220.0f;
    private const float PullBrowserMaxWidth = 340.0f;

    private readonly object stateLock = new();
    private readonly RecapWindow recapWindow;
    private readonly AnalyzerWorkspaceDataController dataController;
    private readonly AnalyzerSessionDataController sessionController;
    private readonly AnalyzerWorkspaceSelection selection = new();
    private readonly IReadOnlyList<IAnalyzerWorkspacePanel> panels = AnalyzerWorkspacePanelCatalog.CreateDefault();
    private readonly Func<string, string, AnalyzerWorkspaceFFLogsImportController> fflogsControllerFactory;

    private IReadOnlyList<PullSummary> pullSummaries = Array.Empty<PullSummary>();
    private AnalyzerWorkspaceLoadedPull? loadedPull;
    private AnalyzerSessionLoaded? loadedSession;
    private CancellationTokenSource? activePullLoadCts;
    private CancellationTokenSource? activeSessionLoadCts;
    private AnalyzerWorkspaceFFLogsImportController? fflogsImportController;
    private string? loadError;
    private string? sessionError;
    private string fflogsClientId = string.Empty;
    private string fflogsClientSecret = string.Empty;
    private string fflogsReportCode = string.Empty;
    private long summaryLoadGeneration;
    private long pullLoadGeneration;
    private long sessionLoadGeneration;
    private bool summaryLoadStarted;
    private bool summariesLoading;
    private bool pullLoading;
    private bool sessionLoading;
    private int selectedPanelIndex;
    private int selectedFFLogsFightIndex = -1;
    private bool disposed;

    public AnalyzerWindow(
        IPullStore pullStore,
        RecapWindow recapWindow,
        Func<string, string, AnalyzerWorkspaceFFLogsImportController> fflogsControllerFactory)
        : base("Better Deaths Analyzer###BetterDeathsAnalyzer")
    {
        ArgumentNullException.ThrowIfNull(pullStore);
        ArgumentNullException.ThrowIfNull(recapWindow);
        ArgumentNullException.ThrowIfNull(fflogsControllerFactory);
        this.recapWindow = recapWindow;
        this.fflogsControllerFactory = fflogsControllerFactory;
        dataController = AnalyzerWorkspaceDataController.CreateDefault(pullStore);
        sessionController = AnalyzerSessionDataController.CreateDefault(pullStore);
        Size = new Vector2(1200.0f, 700.0f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        if (disposed)
        {
            return;
        }

        EnsureSummaryLoadStarted();
        var snapshot = CaptureSnapshot();

        DrawHeader(snapshot);
        ImGui.Separator();

        var available = ImGui.GetContentRegionAvail();
        var browserWidth = Math.Clamp(available.X * 0.25f, PullBrowserMinWidth, PullBrowserMaxWidth);
        if (ImGui.BeginChild("##AnalyzerPullBrowser", new Vector2(browserWidth, 0.0f), true))
        {
            DrawFFLogsImport();
            ImGui.Separator();
            DrawPullBrowser(snapshot);
        }
        ImGui.EndChild();

        ImGui.SameLine();
        if (ImGui.BeginChild("##AnalyzerWorkspace", Vector2.Zero, true))
        {
            DrawWorkspace(snapshot);
        }
        ImGui.EndChild();
    }

    public void Request(AnalyzerWorkspaceNavigationTarget target)
    {
        switch (target)
        {
            case AnalyzerWorkspaceNavigationTarget.LegacyDeaths:
            case AnalyzerWorkspaceNavigationTarget.LegacyReplay:
                if (!recapWindow.FocusLatestPull())
                {
                    recapWindow.IsOpen = true;
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }

    public void OpenEvidence(SessionEvidenceReference evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        selection.SelectPull(evidence.PullId);
        QueuePullLoad(evidence.PullId, evidence.ResultId);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        activePullLoadCts?.Cancel();
        activePullLoadCts?.Dispose();
        activePullLoadCts = null;
        activeSessionLoadCts?.Cancel();
        activeSessionLoadCts?.Dispose();
        activeSessionLoadCts = null;
        sessionController.InvalidatePendingLoad();
        fflogsImportController?.Dispose();
        fflogsImportController = null;
        fflogsClientSecret = string.Empty;
    }

    private void DrawHeader(WorkspaceSnapshot snapshot)
    {
        ImGui.Text("Static Analyzer Workspace");
        ImGui.SameLine();
        if (ImGui.Button("Refresh pulls"))
        {
            QueueSummaryRefresh();
        }

        if (snapshot.LoadedPull is { } loaded)
        {
            ImGui.SameLine();
            var sameSession = snapshot.LoadedSession?.Session.TerritoryId == loaded.Pull.Metadata.TerritoryId;
            if (snapshot.SessionLoading)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button($"{(sameSession ? "Refresh" : "Load")} session##session-load"))
            {
                QueueSessionLoad(loaded.Pull.Metadata.TerritoryId);
            }

            if (snapshot.SessionLoading)
            {
                ImGui.EndDisabled();
            }
        }

        if (snapshot.SummariesLoading)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("Loading pull index...");
        }
        else if (snapshot.PullLoading)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("Loading and analyzing selected pull...");
        }
        else if (snapshot.SessionLoading)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("Loading and analyzing raid session...");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LoadError))
        {
            ImGui.TextWrapped($"Analyzer workspace load error: {snapshot.LoadError}");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.SessionError))
        {
            ImGui.TextWrapped($"Session analysis error: {snapshot.SessionError}");
        }
    }

    private void DrawFFLogsImport()
    {
        ImGui.Text("FFLogs import");
        ImGui.TextDisabled("Public reports use your FFLogs API client credentials.");
        ImGui.SetNextItemWidth(-1.0f);
        ImGui.InputText("##FFLogsClientId", ref fflogsClientId, 256);
        if (string.IsNullOrEmpty(fflogsClientId))
        {
            ImGui.TextDisabled("Client ID");
        }

        ImGui.SetNextItemWidth(-1.0f);
        ImGui.InputText("##FFLogsClientSecret", ref fflogsClientSecret, 512, ImGuiInputTextFlags.Password);
        if (string.IsNullOrEmpty(fflogsClientSecret))
        {
            ImGui.TextDisabled("Client secret (not saved)");
        }

        ImGui.SetNextItemWidth(-1.0f);
        ImGui.InputText("##FFLogsReportCode", ref fflogsReportCode, 128);
        if (string.IsNullOrEmpty(fflogsReportCode))
        {
            ImGui.TextDisabled("Report code");
        }

        var importSnapshot = fflogsImportController?.Snapshot;
        var canLoad = !string.IsNullOrWhiteSpace(fflogsClientId) &&
                      !string.IsNullOrWhiteSpace(fflogsClientSecret) &&
                      !string.IsNullOrWhiteSpace(fflogsReportCode) &&
                      importSnapshot?.IsBusy != true;
        if (!canLoad)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Load fights"))
        {
            QueueFFLogsReportLoad();
        }

        if (!canLoad)
        {
            ImGui.EndDisabled();
        }

        importSnapshot = fflogsImportController?.Snapshot;
        if (importSnapshot?.IsBusy == true)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("Loading...");
        }

        if (importSnapshot?.Error is { } error)
        {
            ImGui.TextWrapped($"FFLogs: {error.SafeMessage}");
        }

        if (importSnapshot?.Report is not { } report)
        {
            return;
        }

        ImGui.TextDisabled($"{report.Fights.Count} fight(s)");
        var fights = report.Fights;
        if (selectedFFLogsFightIndex >= fights.Count)
        {
            selectedFFLogsFightIndex = -1;
        }

        for (var index = 0; index < fights.Count; index++)
        {
            var fight = fights[index];
            var label = $"#{fight.FightId} {fight.Name} ({FormatDuration(fight.Duration)})##fflogs-fight-{fight.FightId}";
            if (ImGui.Selectable(label, selectedFFLogsFightIndex == index))
            {
                selectedFFLogsFightIndex = index;
            }
        }

        var canImport = selectedFFLogsFightIndex >= 0 &&
                        selectedFFLogsFightIndex < fights.Count &&
                        importSnapshot.IsBusy == false;
        if (!canImport)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Import selected fight"))
        {
            QueueFFLogsFightImport(report.ReportCode, fights[selectedFFLogsFightIndex].FightId);
        }

        if (!canImport)
        {
            ImGui.EndDisabled();
        }
    }

    private void QueueFFLogsReportLoad()
    {
        try
        {
            fflogsImportController?.Dispose();
            fflogsImportController = fflogsControllerFactory(fflogsClientId.Trim(), fflogsClientSecret);
            fflogsClientSecret = string.Empty;
            selectedFFLogsFightIndex = -1;
            var controller = fflogsImportController;
            var reportCode = fflogsReportCode.Trim();
            _ = Task.Run(async () =>
            {
                await controller.LoadReportAsync(reportCode).ConfigureAwait(false);
            });
        }
        catch (Exception)
        {
            fflogsClientSecret = string.Empty;
            loadError = "FFLogs credentials could not be initialized.";
        }
    }

    private void QueueFFLogsFightImport(string reportCode, int fightId)
    {
        var controller = fflogsImportController;
        if (controller is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            var result = await controller.ImportFightAsync(reportCode, fightId).ConfigureAwait(false);
            if (!result.Applied || result.Snapshot.ImportedPullId is not { } pullId)
            {
                return;
            }

            selection.SelectPull(pullId);
            QueueSummaryRefresh();
            QueuePullLoad(pullId);
        });
    }

    private void DrawPullBrowser(WorkspaceSnapshot snapshot)
    {
        ImGui.Text("Canonical pulls");
        ImGui.TextDisabled($"Showing up to {PullQueryLimit} locally stored pulls.");
        ImGui.Separator();

        if (snapshot.PullSummaries.Count == 0)
        {
            ImGui.TextDisabled(snapshot.SummariesLoading
                ? "Loading..."
                : "No canonical pulls are stored yet.");
            return;
        }

        foreach (var summary in snapshot.PullSummaries)
        {
            var selected = selection.SelectedPullId == summary.Id;
            if (ImGui.Selectable($"{summary.TerritoryName}##canonical-pull-{summary.Id.Value:N}", selected))
            {
                selection.SelectPull(summary.Id);
                QueuePullLoad(summary.Id);
            }

            ImGui.TextDisabled($"{FormatDuration(summary.Duration)} | {summary.EventCount:N0} events | {summary.SourceKind}");
        }
    }

    private void DrawWorkspace(WorkspaceSnapshot snapshot)
    {
        for (var index = 0; index < panels.Count; index++)
        {
            if (index > 0)
            {
                ImGui.SameLine();
            }

            var panel = panels[index];
            var label = index == selectedPanelIndex
                ? $"[{panel.Label}]##panel-{panel.Id}"
                : $"{panel.Label}##panel-{panel.Id}";
            if (ImGui.Button(label))
            {
                selectedPanelIndex = index;
            }
        }

        ImGui.Separator();
        if (snapshot.LoadedPull is { } loaded)
        {
            if (loaded.Failures.Count > 0)
            {
                ImGui.TextDisabled($"{loaded.Failures.Count} analyzer module(s) failed; independent results remain available.");
            }

            var context = new AnalyzerWorkspacePanelContext
            {
                Selection = selection,
                Pull = loaded.Pull,
                Results = loaded.Results,
                DeathEvents = loaded.DeathEvents,
                Session = snapshot.LoadedSession,
                Navigation = this,
                SessionNavigation = this,
            };
            panels[Math.Clamp(selectedPanelIndex, 0, panels.Count - 1)].Draw(context);
            return;
        }

        ImGui.TextDisabled(snapshot.PullLoading
            ? "Loading selected pull..."
            : "Choose a canonical pull from the browser.");
    }

    private void EnsureSummaryLoadStarted()
    {
        if (summaryLoadStarted)
        {
            return;
        }

        summaryLoadStarted = true;
        QueueSummaryRefresh();
    }

    private void QueueSummaryRefresh()
    {
        long generation;
        lock (stateLock)
        {
            generation = ++summaryLoadGeneration;
            summariesLoading = true;
            loadError = null;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var summaries = await dataController.QueryPullsAsync(PullQueryLimit).ConfigureAwait(false);
                lock (stateLock)
                {
                    if (generation != summaryLoadGeneration)
                    {
                        return;
                    }

                    pullSummaries = summaries;
                    summariesLoading = false;
                }
            }
            catch (Exception exception)
            {
                lock (stateLock)
                {
                    if (generation != summaryLoadGeneration)
                    {
                        return;
                    }

                    summariesLoading = false;
                    loadError = exception.Message;
                }
            }
        });
    }

    private void QueuePullLoad(PullId pullId, AnalysisResultId? focusResultId = null)
    {
        CancellationTokenSource requestCts;
        long generation;
        lock (stateLock)
        {
            activePullLoadCts?.Cancel();
            requestCts = new CancellationTokenSource();
            activePullLoadCts = requestCts;
            generation = ++pullLoadGeneration;
            pullLoading = true;
            loadedPull = null;
            loadError = null;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var loaded = await dataController.LoadPullAsync(pullId, requestCts.Token).ConfigureAwait(false);
                lock (stateLock)
                {
                    if (generation != pullLoadGeneration || requestCts.IsCancellationRequested)
                    {
                        return;
                    }

                    loadedPull = loaded;
                    pullLoading = false;
                    if (loaded is null)
                    {
                        loadError = "The selected canonical pull no longer exists.";
                        return;
                    }

                    if (loadedSession?.Session.TerritoryId != loaded.Pull.Metadata.TerritoryId)
                    {
                        loadedSession = null;
                    }

                    if (focusResultId is { } resultId)
                    {
                        var result = loaded.Results.FirstOrDefault(item => item.Id == resultId);
                        if (result is not null)
                        {
                            selection.SelectResult(result);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (requestCts.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                lock (stateLock)
                {
                    if (generation != pullLoadGeneration)
                    {
                        return;
                    }

                    pullLoading = false;
                    loadError = exception.Message;
                }
            }
            finally
            {
                lock (stateLock)
                {
                    if (ReferenceEquals(activePullLoadCts, requestCts))
                    {
                        activePullLoadCts = null;
                    }
                }

                requestCts.Dispose();
            }
        });
    }

    private void QueueSessionLoad(uint territoryId)
    {
        CancellationTokenSource requestCts;
        long generation;
        lock (stateLock)
        {
            activeSessionLoadCts?.Cancel();
            sessionController.InvalidatePendingLoad();
            requestCts = new CancellationTokenSource();
            activeSessionLoadCts = requestCts;
            generation = ++sessionLoadGeneration;
            sessionLoading = true;
            sessionError = null;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var loaded = await sessionController.LoadAsync(
                    new AnalyzerSessionRequest
                    {
                        TerritoryId = territoryId,
                        Limit = SessionQueryLimit,
                    },
                    cancellationToken: requestCts.Token).ConfigureAwait(false);
                lock (stateLock)
                {
                    if (generation != sessionLoadGeneration || requestCts.IsCancellationRequested)
                    {
                        return;
                    }

                    sessionLoading = false;
                    loadedSession = loaded;
                    if (loaded is null)
                    {
                        sessionError = "The session load was superseded before it completed.";
                    }
                }
            }
            catch (OperationCanceledException) when (requestCts.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                lock (stateLock)
                {
                    if (generation != sessionLoadGeneration)
                    {
                        return;
                    }

                    sessionLoading = false;
                    sessionError = exception.Message;
                }
            }
            finally
            {
                lock (stateLock)
                {
                    if (ReferenceEquals(activeSessionLoadCts, requestCts))
                    {
                        activeSessionLoadCts = null;
                    }
                }

                requestCts.Dispose();
            }
        });
    }

    private WorkspaceSnapshot CaptureSnapshot()
    {
        lock (stateLock)
        {
            return new WorkspaceSnapshot(
                pullSummaries,
                loadedPull,
                loadedSession,
                summariesLoading,
                pullLoading,
                sessionLoading,
                loadError,
                sessionError);
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}";
    }

    private sealed record WorkspaceSnapshot(
        IReadOnlyList<PullSummary> PullSummaries,
        AnalyzerWorkspaceLoadedPull? LoadedPull,
        AnalyzerSessionLoaded? LoadedSession,
        bool SummariesLoading,
        bool PullLoading,
        bool SessionLoading,
        string? LoadError,
        string? SessionError);
}
