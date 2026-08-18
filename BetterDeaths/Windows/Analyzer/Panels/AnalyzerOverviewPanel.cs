namespace BetterDeaths.Windows.Analyzer.Panels;

using Dalamud.Bindings.ImGui;
using System;

internal sealed class AnalyzerOverviewPanel : IAnalyzerWorkspacePanel
{
    public string Id => "overview";

    public string Label => "Overview";

    public void Draw(AnalyzerWorkspacePanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pull = context.Pull;
        if (pull is null)
        {
            ImGui.TextDisabled("Select a recorded pull to inspect it.");
            return;
        }

        ImGui.Text(pull.Metadata.TerritoryName);
        ImGui.TextDisabled($"Duration {FormatTime(pull.Metadata.Duration)} | {pull.Actors.Count} actors | {pull.Events.Count} events");
        ImGui.Separator();

        ImGui.Text("Analysis results");
        if (context.Results.Count == 0)
        {
            ImGui.TextDisabled("No analyzer results are loaded for this pull yet.");
            return;
        }

        foreach (var result in context.Results)
        {
            var selected = context.Selection.SelectedAnalysisResultId == result.Id;
            if (ImGui.Selectable($"[{result.Severity}] {result.Title}##analysis-{result.Id.Value:N}", selected))
            {
                context.Selection.SelectResult(result);
            }

            if (selected && !string.IsNullOrWhiteSpace(result.Summary))
            {
                ImGui.TextWrapped(result.Summary);
            }
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds / 100:0}";
    }
}
