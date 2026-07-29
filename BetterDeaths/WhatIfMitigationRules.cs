using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterDeaths;

internal static class WhatIfMitigationRules
{
    internal static IReadOnlyList<StatusSnapshot> DeduplicateStatuses(IEnumerable<StatusSnapshot> statuses)
    {
        var deduplicated = new List<StatusSnapshot>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var status in statuses)
        {
            if (seen.Add(GetStatusIdentity(status)))
            {
                deduplicated.Add(status);
            }
        }

        return deduplicated;
    }

    internal static bool ShareStatus(
        IReadOnlyList<StatusSnapshot> leftStatuses,
        IReadOnlyList<StatusSnapshot> rightStatuses)
    {
        var leftIdentities = leftStatuses
            .Select(GetStatusIdentity)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return rightStatuses.Any(status => leftIdentities.Contains(GetStatusIdentity(status)));
    }

    private static string GetStatusIdentity(StatusSnapshot status)
    {
        return status.Id == 0
            ? $"name:{status.Name}"
            : $"id:{status.Id}";
    }
}
