namespace BetterDeaths.Windows.Analyzer.Panels;

using BetterDeaths.Domain;
using Dalamud.Bindings.ImGui;
using System;
using System.Linq;

internal sealed class AnalyzerJobsPanel : IAnalyzerWorkspacePanel
{
    public string Id => "jobs";

    public string Label => "Jobs";

    public void Draw(AnalyzerWorkspacePanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pull = context.Pull;
        if (pull is null)
        {
            ImGui.TextDisabled("Select a recorded pull to inspect job execution.");
            return;
        }

        var jobResults = context.Results
            .Where(result => result.Category == AnalysisCategory.Job)
            .OrderBy(result => result.Actors.Count > 0 ? result.Actors[0].Value : int.MaxValue)
            .ThenBy(result => result.TimeRange?.Start ?? TimeSpan.Zero)
            .ThenBy(result => result.Id.Value)
            .ToArray();
        if (jobResults.Length == 0)
        {
            ImGui.TextDisabled("No job-analysis findings are available for this pull.");
            return;
        }

        var selectedPlayer = context.Selection.SelectedActorId is { } selectedActorId &&
                             pull.Actors.Any(actor => actor.Id == selectedActorId && actor.Kind == ActorKind.Player)
            ? selectedActorId
            : (ActorId?)null;
        var visibleResults = selectedPlayer is { } playerId
            ? jobResults.Where(result => result.Actors.Count > 0 && result.Actors[0] == playerId).ToArray()
            : jobResults;

        if (selectedPlayer is { } selectedId)
        {
            var actor = pull.Actors.FirstOrDefault(candidate => candidate.Id == selectedId);
            ImGui.Text(actor is null
                ? "Job findings for selected player"
                : $"Job findings for {actor.Name}{FormatJob(actor)}");
            if (visibleResults.Length == 0)
            {
                ImGui.TextDisabled("No job-analysis findings are attached to the selected player.");
                return;
            }
        }
        else
        {
            ImGui.Text("Job findings");
            ImGui.TextDisabled("Select a finding to synchronize its player and evidence time across the workspace.");
        }

        ImGui.Separator();
        ActorId? lastPrimaryActorId = null;
        foreach (var result in visibleResults)
        {
            var primaryActorId = result.Actors.Count > 0 ? result.Actors[0] : (ActorId?)null;
            if (selectedPlayer is null && primaryActorId != lastPrimaryActorId)
            {
                if (lastPrimaryActorId is not null)
                {
                    ImGui.Spacing();
                }

                var actor = primaryActorId is { } id
                    ? pull.Actors.FirstOrDefault(candidate => candidate.Id == id)
                    : null;
                ImGui.Text(actor is null
                    ? "Unattributed job result"
                    : $"{actor.Name}{FormatJob(actor)}");
                lastPrimaryActorId = primaryActorId;
            }

            var selected = context.Selection.SelectedAnalysisResultId == result.Id;
            var label = $"[{result.Severity}] {result.Title}##job-result-{result.Id.Value:N}";
            if (ImGui.Selectable(label, selected))
            {
                context.Selection.SelectResult(result);
            }

            if (result.TimeRange is { } timeRange)
            {
                ImGui.TextDisabled($"{FormatTime(timeRange.Start)}–{FormatTime(timeRange.End)} | confidence {result.Confidence:P0}");
            }
            else
            {
                ImGui.TextDisabled($"confidence {result.Confidence:P0}");
            }

            if (selected && !string.IsNullOrWhiteSpace(result.Summary))
            {
                ImGui.TextWrapped(result.Summary);
                var evidenceCount = result.Evidence.Sum(evidence => evidence.EventIds.Count);
                ImGui.TextDisabled($"{evidenceCount:N0} linked event evidence reference(s)");
            }
        }
    }

    private static string FormatJob(ActorRecord actor)
    {
        return string.IsNullOrWhiteSpace(actor.JobAbbreviation)
            ? string.Empty
            : $" ({actor.JobAbbreviation.Trim().ToUpperInvariant()})";
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds / 100:0}";
    }
}
