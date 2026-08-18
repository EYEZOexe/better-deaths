namespace BetterDeaths.Windows.Analyzer.Panels;

using BetterDeaths.Domain;
using Dalamud.Bindings.ImGui;
using System;

internal sealed class AnalyzerTimelinePanel : IAnalyzerWorkspacePanel
{
    private const int ShellEventDisplayLimit = 250;

    public string Id => "timeline";

    public string Label => "Timeline";

    public void Draw(AnalyzerWorkspacePanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pull = context.Pull;
        if (pull is null)
        {
            ImGui.TextDisabled("Select a recorded pull to view its timeline.");
            return;
        }

        DrawSelectedTime(context.Selection.SelectedTimeRange);
        ImGui.Separator();

        var count = Math.Min(pull.Events.Count, ShellEventDisplayLimit);
        for (var index = 0; index < count; index++)
        {
            var evt = pull.Events[index];
            var selected = Contains(context.Selection.SelectedTimeRange, evt.PullTime);
            var label = $"{FormatTime(evt.PullTime)}  {GetEventLabel(evt)}##event-{evt.Id.Value}";
            if (ImGui.Selectable(label, selected))
            {
                context.Selection.SelectTime(new TimeRange(evt.PullTime, evt.PullTime));
            }
        }

        if (pull.Events.Count > ShellEventDisplayLimit)
        {
            ImGui.TextDisabled($"Shell view shows the first {ShellEventDisplayLimit} of {pull.Events.Count} events. Full timeline virtualization belongs to later workspace refinement.");
        }
    }

    private static void DrawSelectedTime(TimeRange? selectedTime)
    {
        if (selectedTime is null)
        {
            ImGui.TextDisabled("No synchronized time selection.");
            return;
        }

        var range = selectedTime.Value;
        var label = range.Start == range.End
            ? FormatTime(range.Start)
            : $"{FormatTime(range.Start)} - {FormatTime(range.End)}";
        ImGui.Text($"Selected time: {label}");
    }

    private static bool Contains(TimeRange? range, TimeSpan time)
    {
        return range is { } value && time >= value.Start && time <= value.End;
    }

    private static string GetEventLabel(NormalizedEvent evt)
    {
        return evt switch
        {
            DamageEvent damage => $"Damage  action {damage.ActionId}  {damage.Amount:N0}",
            HealEvent heal => $"Heal  action {heal.ActionId}  {heal.Amount:N0}",
            CastStartEvent cast => $"Cast start  action {cast.ActionId}",
            CastEndEvent cast => $"Cast end  action {cast.ActionId}",
            ActionUseEvent action => $"Action  {action.ActionId}",
            StatusApplyEvent status => $"Status +{status.StatusId}",
            StatusRemoveEvent status => $"Status -{status.StatusId}",
            DeathEvent => "Death",
            RaiseEvent => "Raise",
            TargetabilityEvent targetability => targetability.IsTargetable ? "Targetable" : "Untargetable",
            _ => evt.GetType().Name,
        };
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds / 100:0}";
    }
}
