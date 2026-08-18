namespace BetterDeaths.Windows.Analyzer.Panels;

using BetterDeaths.Domain;
using Dalamud.Bindings.ImGui;
using System;

internal sealed class AnalyzerReplayPanel : IAnalyzerWorkspacePanel
{
    public string Id => "replay";

    public string Label => "Replay";

    public void Draw(AnalyzerWorkspacePanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pull = context.Pull;
        if (pull is null)
        {
            ImGui.TextDisabled("Select a recorded pull to inspect replay coverage.");
            return;
        }

        ImGui.Text("Replay evidence");
        ImGui.TextDisabled($"{pull.Positions.Count} position samples | {pull.WorldMarkers.Count} world-marker samples");

        if (context.Selection.SelectedTimeRange is { } selectedTime)
        {
            ImGui.Text($"Synchronized time: {FormatRange(selectedTime)}");
        }
        else
        {
            ImGui.TextDisabled("No synchronized replay time selected yet.");
        }

        if (context.Selection.SelectedActorId is { } actorId)
        {
            ImGui.Text($"Focused actor: {actorId.Value}");
        }

        ImGui.Separator();
        ImGui.TextWrapped("This M4 shell shares pull/actor/time selection with Timeline and Deaths. The existing Better Deaths replay remains the detailed renderer until it is incrementally bridged into the analyzer workspace.");

        if (ImGui.Button("Open legacy replay review"))
        {
            context.Navigation?.Request(AnalyzerWorkspaceNavigationTarget.LegacyReplay);
        }

        if (context.Navigation is null)
        {
            ImGui.TextDisabled("Legacy replay navigation will be connected by AnalyzerWindow in M4-C.");
        }
    }

    private static string FormatRange(TimeRange range)
    {
        return range.Start == range.End
            ? FormatTime(range.Start)
            : $"{FormatTime(range.Start)} - {FormatTime(range.End)}";
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds / 100:0}";
    }
}
