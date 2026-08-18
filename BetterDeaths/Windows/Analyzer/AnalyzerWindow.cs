namespace BetterDeaths.Windows.Analyzer;

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

internal sealed class AnalyzerWindow : Window, IDisposable, IAnalyzerWorkspaceNavigation
{
    private const int PullQueryLimit = 100;
    private const float PullBrowserMinWidth = 220.0f;
    private const float PullBrowserMaxWidth = 340.0f;

    private readonly object stateLock = new();
    private readonly RecapWindow recapWindow;
    private readonly AnalyzerWorkspaceDataController dataController;
    private readonly AnalyzerWorkspaceSelection selection = new();
    private readonly IReadOnlyList<IAnalyzerWorkspacePanel> panels = AnalyzerWorkspacePanelCatalog.CreateDefault();
    private readonly CancellationTokenSource lifetimeCts = new();

    private IReadOnlyList<PullSummary> pullSummaries = Array.Empty<PullSummary>();
    private AnalyzerWorkspaceLoadedPull? loadedPull;
    private CancellationTokenSource? activePullLoadCts;
    private string? loadError;
    private long summaryLoadGeneration;
    private long pullLoadGeneration;
    private bool summaryLoadStarted;
    private bool summariesLoading;
    private bool pullLoading;
    private int selectedPanelIndex;

    public AnalyzerWindow(IPullStore pullStore, RecapWindow recapWindow)
        : base("Better Deaths Analyzer###BetterDeathsAnalyzer")
    {
        ArgumentNullException.ThrowIfNull(pullStore);
        ArgumentNullException.ThrowIfNull(recapWindow);
        this.recapWindow = recapWindow;
        dataController = AnalyzerWorkspaceDataController.CreateDefault(pullStore);
        Size = new Vector2(1200.0f, 700.0f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose()
    {
        lifetimeCts.Cancel();
        lock (stateLock)
        {
            activePullLoadCts?.Cancel();
        }

        lifetimeCts.Dispose();
    }

    public override void Draw()
    {
        EnsureSummaryLoadStarted();
        var snapshot = CaptureSnapshot();

        DrawHeader(snapshot);
        ImGui.Separator();

        var available = ImGui.GetContentRegionAvail();
        var browserWidth = Math.Clamp(available.X * 0.25f, PullBrowserMinWidth, PullBrowserMaxWidth);
        if (ImGui.BeginChild("##AnalyzerPullBrowser", new Vector2(browserWidth, 0.0f), true))
        {
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
                if (!recapWindow.FocusLatestPull())
                {
                    recapWindow.IsOpen = true;
                }
                break;
            case AnalyzerWorkspaceNavigationTarget.LegacyReplay:
                if (!recapWindow.FocusLatestReplay())
                {
                    recapWindow.IsOpen = true;
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }

    private void DrawHeader(WorkspaceSnapshot snapshot)
    {
        ImGui.Text("Static Analyzer Workspace");
        ImGui.SameLine();
        if (ImGui.Button("Refresh pulls"))
        {
            QueueSummaryRefresh();
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

        if (!string.IsNullOrWhiteSpace(snapshot.LoadError))
        {
            ImGui.TextWrapped($"Analyzer workspace load error: {snapshot.LoadError}");
        }
    }

    private void DrawPullBrowser(WorkspaceSnapshot snapshot)
    {
        ImGui.Text("Canonical pulls");
        ImGui.TextDisabled($"Showing up to {PullQueryLimit} locally recorded pulls.");
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
                Navigation = this,
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
                var summaries = await dataController
                    .QueryPullsAsync(PullQueryLimit, lifetimeCts.Token)
                    .ConfigureAwait(false);
                lock (stateLock)
                {
                    if (generation != summaryLoadGeneration || lifetimeCts.IsCancellationRequested)
                    {
                        return;
                    }

                    pullSummaries = summaries;
                    summariesLoading = false;
                }
            }
            catch (OperationCanceledException) when (lifetimeCts.IsCancellationRequested)
            {
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

    private void QueuePullLoad(PullId pullId)
    {
        CancellationTokenSource requestCts;
        long generation;
        lock (stateLock)
        {
            activePullLoadCts?.Cancel();
            requestCts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCts.Token);
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

    private WorkspaceSnapshot CaptureSnapshot()
    {
        lock (stateLock)
        {
            return new WorkspaceSnapshot(
                pullSummaries,
                loadedPull,
                summariesLoading,
                pullLoading,
                loadError);
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}";
    }

    private sealed record WorkspaceSnapshot(
        IReadOnlyList<PullSummary> PullSummaries,
        AnalyzerWorkspaceLoadedPull? LoadedPull,
        bool SummariesLoading,
        bool PullLoading,
        string? LoadError);
}
