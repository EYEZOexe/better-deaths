namespace BetterDeaths;

using System;
using System.Linq;

internal static class RecordedPullLookup
{
    private static readonly TimeSpan LegacyTimestampTolerance = TimeSpan.FromSeconds(10);

    public static bool MayContainDeath(
        RecordedPullSummary summary,
        long deathSeenAtUtcTicks,
        uint memberKeyHash)
    {
        if (summary.DeathReferencesIndexed)
        {
            return summary.DeathReferences.Any(reference =>
                reference.SeenAtUtcTicks == deathSeenAtUtcTicks &&
                reference.MemberKeyHash == memberKeyHash);
        }

        var capturedAtTicks = summary.CapturedAtUtc.Ticks;
        var durationTicks = TimeSpan.FromSeconds(Math.Max(0.0f, summary.PullElapsedSeconds)).Ticks;
        var toleranceTicks = LegacyTimestampTolerance.Ticks;
        return deathSeenAtUtcTicks >= capturedAtTicks - durationTicks - toleranceTicks &&
            deathSeenAtUtcTicks <= capturedAtTicks + toleranceTicks;
    }
}
