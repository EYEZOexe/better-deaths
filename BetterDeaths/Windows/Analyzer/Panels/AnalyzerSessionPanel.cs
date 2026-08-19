namespace BetterDeaths.Windows.Analyzer.Panels;

using BetterDeaths.Analysis.Sessions;
using Dalamud.Bindings.ImGui;
using System;
using System.Linq;

internal sealed class AnalyzerSessionPanel : IAnalyzerWorkspacePanel
{
    private const int MaxVisibleRecurrences = 100;
    private const int MaxVisibleWipeCauses = 50;

    public string Id => "session";

    public string Label => "Session";

    public void Draw(AnalyzerWorkspacePanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Session is not { } loaded)
        {
            ImGui.TextDisabled("Load a raid session for the selected encounter to inspect cross-pull intelligence.");
            return;
        }

        var analysis = loaded.Analysis;
        DrawHeader(loaded);
        ImGui.Separator();
        DrawProgression(analysis.Progression);
        ImGui.Separator();
        DrawRecurrences(context, analysis);
        ImGui.Separator();
        DrawWipeCauses(context, analysis.WipeCauses);
        ImGui.Separator();
        DrawTrends(analysis);

        if (analysis.Diagnostics.UnkeyedActionableResultCount > 0 ||
            analysis.Diagnostics.InvalidWipeCauseReferenceCount > 0 ||
            loaded.Diagnostics.Count > 0)
        {
            ImGui.Separator();
            ImGui.Text("Session diagnostics");
            ImGui.TextDisabled(
                $"{loaded.Diagnostics.Count:N0} load/analyzer diagnostic(s) | " +
                $"{analysis.Diagnostics.UnkeyedActionableResultCount:N0} actionable result(s) without recurrence keys | " +
                $"{analysis.Diagnostics.InvalidWipeCauseReferenceCount:N0} invalid wipe-cause reference(s)");
        }
    }

    private static void DrawHeader(AnalyzerSessionLoaded loaded)
    {
        ImGui.Text($"{loaded.Session.TerritoryName} session");
        ImGui.TextDisabled(
            $"{loaded.Session.Pulls.Count:N0} analyzed / {loaded.SelectedPullCount:N0} selected pull(s) | " +
            $"{loaded.Analysis.WipeCauses.TotalWipes:N0} explicit wipe(s)");
    }

    private static void DrawProgression(SessionProgressionSummary progression)
    {
        ImGui.Text("Progression");
        ImGui.TextDisabled(
            $"{progression.EvaluablePullCount:N0} pull(s) with phase evidence | {progression.UnknownPullCount:N0} unknown");

        if (progression.Phases.Count == 0)
        {
            ImGui.TextDisabled("No explicit phase-reach evidence is available for this session.");
            return;
        }

        foreach (var phase in progression.Phases.OrderBy(phase => phase.PhaseOrder).ThenBy(phase => phase.PhaseKey, StringComparer.Ordinal))
        {
            var rate = phase.ReachRate is { } value ? value.ToString("P1") : "unknown";
            var timing = phase.AverageReachedAt is { } reachedAt
                ? $" | avg {FormatTime(reachedAt)}"
                : string.Empty;
            ImGui.Text($"{phase.PhaseKey}: {phase.ReachedPullCount:N0}/{phase.EvaluablePullCount:N0} ({rate}){timing}");
        }
    }

    private static void DrawRecurrences(
        AnalyzerWorkspacePanelContext context,
        SessionIntelligenceResult analysis)
    {
        ImGui.Text("Recurring findings");
        ImGui.TextDisabled("Rates use known opportunities only; unknown opportunities are shown separately.");

        var rows = analysis.Recurrences.Take(MaxVisibleRecurrences).ToArray();
        if (rows.Length == 0)
        {
            ImGui.TextDisabled("No keyed recurring findings/opportunities are available yet.");
            return;
        }

        foreach (var row in rows)
        {
            var participant = row.Key.ParticipantKey is { } participantKey
                ? $" | {participantKey.Value}"
                : string.Empty;
            var rate = row.Counts.Rate is { } value ? value.ToString("P1") : "unknown";
            ImGui.Text(
                $"{row.Key.FindingKey.RuleKey}{participant}: " +
                $"{row.Counts.FindingCount:N0}/{row.Counts.OpportunityCount:N0} ({rate})");
            ImGui.SameLine();
            ImGui.TextDisabled($"unknown {row.Counts.UnknownCount:N0}");

            if (row.Evidence.Count > 0 && context.SessionNavigation is { } navigation)
            {
                ImGui.SameLine();
                var evidence = row.Evidence[^1];
                if (ImGui.SmallButton($"Open evidence##session-recurrence-{row.Key}"))
                {
                    navigation.OpenEvidence(evidence);
                }
            }
        }

        if (analysis.Recurrences.Count > rows.Length)
        {
            ImGui.TextDisabled($"Showing the first {rows.Length:N0} of {analysis.Recurrences.Count:N0} recurrence rows.");
        }
    }

    private static void DrawWipeCauses(
        AnalyzerWorkspacePanelContext context,
        SessionWipeCauseSummary wipeCauses)
    {
        ImGui.Text("Wipe causes");
        ImGui.TextDisabled(
            $"{wipeCauses.KnownCauseWipes:N0} explicit known | {wipeCauses.UnknownCauseWipes:N0} unknown of {wipeCauses.TotalWipes:N0} wipe(s)");

        var rows = wipeCauses.Causes.Take(MaxVisibleWipeCauses).ToArray();
        if (rows.Length == 0)
        {
            ImGui.TextDisabled("No explicit evidence-backed wipe cause is available; unknown wipes are not guessed from death chronology.");
            return;
        }

        foreach (var cause in rows)
        {
            ImGui.Text($"{cause.Key.FindingKey.RuleKey}: {cause.WipeCount:N0} wipe(s)");
            if (cause.Evidence.Count > 0 && context.SessionNavigation is { } navigation)
            {
                ImGui.SameLine();
                var evidence = cause.Evidence[^1];
                if (ImGui.SmallButton($"Open evidence##session-wipe-{cause.Key}"))
                {
                    navigation.OpenEvidence(evidence);
                }
            }
        }
    }

    private static void DrawTrends(SessionIntelligenceResult analysis)
    {
        ImGui.Text("Recent trend");
        var trends = analysis.Trends
            .Where(trend => trend.Direction != SessionTrendDirection.InsufficientEvidence)
            .Take(MaxVisibleRecurrences)
            .ToArray();
        if (trends.Length == 0)
        {
            ImGui.TextDisabled("No recurrence has enough known opportunities in both comparison windows yet.");
            return;
        }

        foreach (var trend in trends)
        {
            var prior = trend.Prior.Rate?.ToString("P1") ?? "unknown";
            var recent = trend.Recent.Rate?.ToString("P1") ?? "unknown";
            ImGui.Text($"{trend.Key.FindingKey.RuleKey}: {trend.Direction} | prior {prior} -> recent {recent}");
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }
}
