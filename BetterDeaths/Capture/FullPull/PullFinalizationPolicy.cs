namespace BetterDeaths.Capture.FullPull;

using System;

internal readonly record struct PullFinalizationFacts(
    bool DutyActive,
    bool CombatObserved,
    int RelevantEventCount,
    TimeSpan Duration);

internal static class PullFinalizationPolicy
{
    internal static readonly TimeSpan MinimumMeaningfulDuration = TimeSpan.FromSeconds(1);

    internal const int MinimumRelevantEventCount = 1;

    public static bool IsMeaningful(PullFinalizationFacts facts)
    {
        return facts.DutyActive &&
            facts.CombatObserved &&
            facts.RelevantEventCount >= MinimumRelevantEventCount &&
            facts.Duration >= MinimumMeaningfulDuration;
    }
}
