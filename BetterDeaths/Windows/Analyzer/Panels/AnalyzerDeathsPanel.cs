namespace BetterDeaths.Windows.Analyzer.Panels;

using BetterDeaths.Domain;
using Dalamud.Bindings.ImGui;
using System;
using System.Linq;

internal sealed class AnalyzerDeathsPanel : IAnalyzerWorkspacePanel
{
    public string Id => "deaths";

    public string Label => "Deaths";

    public void Draw(AnalyzerWorkspacePanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pull = context.Pull;
        if (pull is null)
        {
            ImGui.TextDisabled("Select a recorded pull to inspect deaths.");
            return;
        }

        ImGui.Text("Canonical death events");
        ImGui.TextDisabled("Causal death analysis remains a later M5 analyzer; this panel only navigates recorded evidence.");
        ImGui.Separator();

        if (context.DeathEvents.Count == 0)
        {
            ImGui.TextDisabled("No canonical deaths were recorded for this pull.");
        }
        else
        {
            foreach (var death in context.DeathEvents)
            {
                var actorName = ResolveActorName(pull, death.TargetActorId);
                var selected = Contains(context.Selection.SelectedTimeRange, death.PullTime);
                if (ImGui.Selectable($"{FormatTime(death.PullTime)}  {actorName}##death-{death.Id.Value}", selected))
                {
                    context.Selection.SelectTime(new TimeRange(death.PullTime, death.PullTime));
                    context.Selection.SelectActor(death.TargetActorId);
                }
            }
        }

        ImGui.Separator();
        if (ImGui.Button("Open legacy death review"))
        {
            context.Navigation?.Request(AnalyzerWorkspaceNavigationTarget.LegacyDeaths);
        }

        if (context.Navigation is null)
        {
            ImGui.TextDisabled("Legacy death-review navigation will be connected by AnalyzerWindow in M4-C.");
        }
    }

    private static string ResolveActorName(RecordedPull pull, ActorId? actorId)
    {
        if (actorId is null)
        {
            return "Unknown actor";
        }

        return pull.Actors.FirstOrDefault(actor => actor.Id == actorId.Value)?.Name ?? $"Actor {actorId.Value.Value}";
    }

    private static bool Contains(TimeRange? range, TimeSpan time)
    {
        return range is { } value && time >= value.Start && time <= value.End;
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds / 100:0}";
    }
}
