namespace BetterDeaths.Analysis.Encounters;

using BetterDeaths.Domain;
using System;

internal enum EncounterPartyRole
{
    Unknown,
    Tank,
    Healer,
    Melee,
    Ranged,
}

internal static class EncounterPartyRoleResolver
{
    public static EncounterPartyRole Resolve(ActorRecord actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind != ActorKind.Player)
        {
            return EncounterPartyRole.Unknown;
        }

        return Resolve(actor.JobAbbreviation);
    }

    public static EncounterPartyRole Resolve(string? jobAbbreviation)
    {
        var job = Normalize(jobAbbreviation);
        return job switch
        {
            "PLD" or "PALADIN" or
            "WAR" or "WARRIOR" or
            "DRK" or "DARKKNIGHT" or
            "GNB" or "GUNBREAKER" => EncounterPartyRole.Tank,

            "WHM" or "WHITEMAGE" or
            "SCH" or "SCHOLAR" or
            "AST" or "ASTROLOGIAN" or
            "SGE" or "SAGE" => EncounterPartyRole.Healer,

            "MNK" or "MONK" or
            "DRG" or "DRAGOON" or
            "NIN" or "NINJA" or
            "SAM" or "SAMURAI" or
            "RPR" or "REAPER" or
            "VPR" or "VIPER" => EncounterPartyRole.Melee,

            "BRD" or "BARD" or
            "MCH" or "MACHINIST" or
            "DNC" or "DANCER" or
            "BLM" or "BLACKMAGE" or
            "SMN" or "SUMMONER" or
            "RDM" or "REDMAGE" or
            "PCT" or "PICTOMANCER" => EncounterPartyRole.Ranged,

            _ => EncounterPartyRole.Unknown,
        };
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
