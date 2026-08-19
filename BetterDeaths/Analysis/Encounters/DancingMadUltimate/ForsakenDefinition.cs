namespace BetterDeaths.Analysis.Encounters.DancingMadUltimate;

using System;
using System.Collections.Generic;

// Strategy provenance:
// EYEZOexe/wtfdig @ 73a2ffa959b8f57bfbe7a1a75d5e43383ae2ea81
// - src/routes/ultimates/umad/data.ts (P2 Forsaken / Kroxy-Rinon opening group rule)
// - src/lib/arena.ts (role/group vocabulary)
// See docs/analyzer/M8_WTFDIG_AUDIT.md and THIRD_PARTY_NOTICES.md.
internal static class ForsakenDefinition
{
    public const uint TerritoryId = 1363;
    public const string EncounterKey = "dancing-mad-ultimate";
    public const string PhaseKey = "p2-forsaken";
    public const string RolePartnerRuleKey = "forsaken-role-partner";
    public const string GroupRuleKey = "forsaken-opening-group";

    // These canonical status IDs come from the existing Better Deaths DMU replay catalog, not WTFDiG.
    // The new encounter analyzer consumes StatusApplyEvent rather than legacy replay marker types.
    public const uint StackStatusId = 5084;
    public const uint SpreadStatusId = 5085;
    public const uint ConeStatusId = 5086;

    public static EncounterDefinition Encounter { get; } = new(
        EncounterKey,
        "Dancing Mad Ultimate",
        TerritoryId,
        new ArenaGeometry
        {
            Shape = ArenaShape.Circle,
            CenterX = 100.0f,
            CenterY = 100.0f,
            RadiusOrHalfSize = 20.0f,
        },
        phases:
        [
            new EncounterPhaseDefinition
            {
                Key = PhaseKey,
                DisplayName = "P2 - Forsaken",
            },
        ],
        assignmentRules:
        [
            new AssignmentRule
            {
                Key = RolePartnerRuleKey,
                Description =
                    "Kroxy-Rinon opening role/group partners are evaluated as Tank↔Healer and Melee↔Ranged pair candidates; slot labels are not invented when the canonical pull does not provide them.",
            },
            new AssignmentRule
            {
                Key = GroupRuleKey,
                Description =
                    "Forsaken opening pair: Group A when the two debuffs differ and one is Stack; Group B when both debuffs are the same.",
            },
        ]);

    public static IReadOnlySet<uint> RelevantStatusIds { get; } = new HashSet<uint>
    {
        StackStatusId,
        SpreadStatusId,
        ConeStatusId,
    };

    public static ForsakenDebuffKind GetDebuffKind(uint statusId)
    {
        return statusId switch
        {
            StackStatusId => ForsakenDebuffKind.Stack,
            SpreadStatusId => ForsakenDebuffKind.Spread,
            ConeStatusId => ForsakenDebuffKind.Cone,
            _ => ForsakenDebuffKind.Unknown,
        };
    }

    public static bool ArePartnerRolesCompatible(EncounterPartyRole first, EncounterPartyRole second)
    {
        return first == EncounterPartyRole.Tank && second == EncounterPartyRole.Healer ||
               first == EncounterPartyRole.Healer && second == EncounterPartyRole.Tank ||
               first == EncounterPartyRole.Melee && second == EncounterPartyRole.Ranged ||
               first == EncounterPartyRole.Ranged && second == EncounterPartyRole.Melee;
    }

    public static ForsakenPairGroup ClassifyOpeningPair(ForsakenDebuffKind first, ForsakenDebuffKind second)
    {
        if (first == ForsakenDebuffKind.Unknown || second == ForsakenDebuffKind.Unknown)
        {
            return ForsakenPairGroup.Unknown;
        }

        if (first == second)
        {
            return ForsakenPairGroup.GroupB;
        }

        if (first == ForsakenDebuffKind.Stack || second == ForsakenDebuffKind.Stack)
        {
            return ForsakenPairGroup.GroupA;
        }

        return ForsakenPairGroup.Incompatible;
    }
}

internal enum ForsakenDebuffKind
{
    Unknown,
    Stack,
    Spread,
    Cone,
}

internal enum ForsakenPairGroup
{
    Unknown,
    GroupA,
    GroupB,
    Incompatible,
}
