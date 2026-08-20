namespace BetterDeaths.Sources.FFLogs;

using BetterDeaths.Domain;
using System;
using System.Collections.Generic;

internal static class FFLogsJobIdentityMapper
{
    private static readonly IReadOnlyDictionary<string, string> KnownIdentities =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Paladin"] = "PLD",
            ["PLD"] = "PLD",
            ["Warrior"] = "WAR",
            ["WAR"] = "WAR",
            ["DarkKnight"] = "DRK",
            ["Dark Knight"] = "DRK",
            ["dark-knight"] = "DRK",
            ["DRK"] = "DRK",
            ["Gunbreaker"] = "GNB",
            ["GNB"] = "GNB",
            ["WhiteMage"] = "WHM",
            ["White Mage"] = "WHM",
            ["white-mage"] = "WHM",
            ["WHM"] = "WHM",
            ["Scholar"] = "SCH",
            ["SCH"] = "SCH",
            ["Astrologian"] = "AST",
            ["AST"] = "AST",
            ["Sage"] = "SGE",
            ["SGE"] = "SGE",
            ["Monk"] = "MNK",
            ["MNK"] = "MNK",
            ["Dragoon"] = "DRG",
            ["DRG"] = "DRG",
            ["Ninja"] = "NIN",
            ["NIN"] = "NIN",
            ["Samurai"] = "SAM",
            ["SAM"] = "SAM",
            ["Reaper"] = "RPR",
            ["RPR"] = "RPR",
            ["Viper"] = "VPR",
            ["VPR"] = "VPR",
            ["Bard"] = "BRD",
            ["BRD"] = "BRD",
            ["Machinist"] = "MCH",
            ["MCH"] = "MCH",
            ["Dancer"] = "DNC",
            ["DNC"] = "DNC",
            ["BlackMage"] = "BLM",
            ["Black Mage"] = "BLM",
            ["black-mage"] = "BLM",
            ["BLM"] = "BLM",
            ["Summoner"] = "SMN",
            ["SMN"] = "SMN",
            ["RedMage"] = "RDM",
            ["Red Mage"] = "RDM",
            ["red-mage"] = "RDM",
            ["RDM"] = "RDM",
            ["Pictomancer"] = "PCT",
            ["PCT"] = "PCT",
        };

    public static string? ToCanonicalAbbreviation(ActorKind actorKind, string? jobSubType)
    {
        if (actorKind != ActorKind.Player ||
            string.IsNullOrWhiteSpace(jobSubType))
        {
            return null;
        }

        return KnownIdentities.TryGetValue(jobSubType.Trim(), out var abbreviation)
            ? abbreviation
            : null;
    }
}
