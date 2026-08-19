namespace BetterDeaths.Windows.Analyzer.Panels;

using BetterDeaths.Domain;
using Dalamud.Bindings.ImGui;
using System;
using System.Linq;

internal sealed class AnalyzerMechanicsPanel : IAnalyzerWorkspacePanel
{
    public string Id => "mechanics";

    public string Label => "Mechanics";

    public void Draw(AnalyzerWorkspacePanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Pull is null)
        {
            ImGui.TextDisabled("Select a recorded pull to inspect encounter mechanics.");
            return;
        }

        var mechanics = context.Results
            .Where(result => result.Category == AnalysisCategory.Mechanic)
            .OrderBy(result => result.TimeRange?.Start ?? TimeSpan.Zero)
            .ThenBy(result => result.Severity)
            .ThenBy(result => result.Id.Value)
            .ToArray();
        if (mechanics.Length == 0)
        {
            ImGui.TextDisabled("No encounter-mechanic findings are available for this pull.");
            return;
        }

        var selectedActorId = context.Selection.SelectedActorId;
        var visible = selectedActorId is { } actorId
            ? mechanics.Where(result => result.Actors.Contains(actorId)).ToArray()
            : mechanics;
        if (selectedActorId is not null && visible.Length == 0)
        {
            ImGui.TextDisabled("No encounter-mechanic findings are linked to the selected actor.");
            return;
        }

        ImGui.Text("Encounter mechanics");
        ImGui.TextDisabled("Select a finding to synchronize its actors and evidence time across the workspace.");
        ImGui.Separator();

        foreach (var result in visible)
        {
            var selected = context.Selection.SelectedAnalysisResultId == result.Id;
            var label = $"[{result.Severity}] {result.Title}##mechanic-result-{result.Id.Value:N}";
            if (ImGui.Selectable(label, selected))
            {
                context.Selection.SelectResult(result);
            }

            if (result.TimeRange is { } range)
            {
                ImGui.TextDisabled($"{FormatTime(range.Start)}–{FormatTime(range.End)} | confidence {result.Confidence:P0}");
            }
            else
            {
                ImGui.TextDisabled($"confidence {result.Confidence:P0}");
            }

            if (selected)
            {
                if (!string.IsNullOrWhiteSpace(result.Summary))
                {
                    ImGui.TextWrapped(result.Summary);
                }

                var evidenceCount = result.Evidence.Sum(evidence => evidence.EventIds.Count);
                var actorCount = result.Actors.Count;
                ImGui.TextDisabled($"{actorCount:N0} linked actor(s) | {evidenceCount:N0} event evidence reference(s)");
            }
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds / 100:0}";
    }
}
