namespace BetterDeaths.Analysis.Encounters;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

internal enum ArenaShape
{
    Circle,
    Square,
}

internal sealed record ArenaGeometry
{
    public required ArenaShape Shape { get; init; }

    public required float CenterX { get; init; }

    public required float CenterY { get; init; }

    public required float RadiusOrHalfSize { get; init; }

    public void Validate()
    {
        if (!float.IsFinite(CenterX) || !float.IsFinite(CenterY))
        {
            throw new ArgumentOutOfRangeException(nameof(ArenaGeometry), "Arena center must be finite.");
        }

        if (!float.IsFinite(RadiusOrHalfSize) || RadiusOrHalfSize <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(ArenaGeometry), "Arena radius/half-size must be positive and finite.");
        }
    }
}

internal sealed record EncounterPhaseDefinition
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }
}

internal sealed record AssignmentRule
{
    public required string Key { get; init; }

    public required string Description { get; init; }
}

internal sealed class EncounterDefinition
{
    private readonly IReadOnlyDictionary<string, EncounterPhaseDefinition> phasesByKey;
    private readonly IReadOnlyDictionary<string, AssignmentRule> assignmentRulesByKey;

    public EncounterDefinition(
        string key,
        string displayName,
        uint territoryId,
        ArenaGeometry arena,
        IEnumerable<EncounterPhaseDefinition> phases,
        IEnumerable<AssignmentRule> assignmentRules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(phases);
        ArgumentNullException.ThrowIfNull(assignmentRules);
        if (territoryId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(territoryId));
        }

        arena.Validate();
        var phaseArray = phases.ToArray();
        var ruleArray = assignmentRules.ToArray();
        ValidatePhases(phaseArray);
        ValidateRules(ruleArray);

        Key = key.Trim();
        DisplayName = displayName.Trim();
        TerritoryId = territoryId;
        Arena = arena;
        Phases = Array.AsReadOnly(phaseArray);
        AssignmentRules = Array.AsReadOnly(ruleArray);
        phasesByKey = new ReadOnlyDictionary<string, EncounterPhaseDefinition>(
            phaseArray.ToDictionary(phase => phase.Key, StringComparer.Ordinal));
        assignmentRulesByKey = new ReadOnlyDictionary<string, AssignmentRule>(
            ruleArray.ToDictionary(rule => rule.Key, StringComparer.Ordinal));
    }

    public string Key { get; }

    public string DisplayName { get; }

    public uint TerritoryId { get; }

    public ArenaGeometry Arena { get; }

    public IReadOnlyList<EncounterPhaseDefinition> Phases { get; }

    public IReadOnlyList<AssignmentRule> AssignmentRules { get; }

    public EncounterPhaseDefinition Phase(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return phasesByKey.TryGetValue(key, out var phase)
            ? phase
            : throw new KeyNotFoundException($"Encounter '{Key}' does not contain phase '{key}'.");
    }

    public AssignmentRule AssignmentRule(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return assignmentRulesByKey.TryGetValue(key, out var rule)
            ? rule
            : throw new KeyNotFoundException($"Encounter '{Key}' does not contain assignment rule '{key}'.");
    }

    private static void ValidatePhases(IReadOnlyList<EncounterPhaseDefinition> phases)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var phase in phases)
        {
            ArgumentNullException.ThrowIfNull(phase);
            ArgumentException.ThrowIfNullOrWhiteSpace(phase.Key);
            ArgumentException.ThrowIfNullOrWhiteSpace(phase.DisplayName);
            if (!keys.Add(phase.Key))
            {
                throw new ArgumentException($"Duplicate encounter phase key '{phase.Key}'.", nameof(phases));
            }
        }
    }

    private static void ValidateRules(IReadOnlyList<AssignmentRule> rules)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            ArgumentNullException.ThrowIfNull(rule);
            ArgumentException.ThrowIfNullOrWhiteSpace(rule.Key);
            ArgumentException.ThrowIfNullOrWhiteSpace(rule.Description);
            if (!keys.Add(rule.Key))
            {
                throw new ArgumentException($"Duplicate assignment rule key '{rule.Key}'.", nameof(rules));
            }
        }
    }
}
