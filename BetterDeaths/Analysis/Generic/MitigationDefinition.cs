namespace BetterDeaths.Analysis.Generic;

using System;

internal enum MitigationApplicationKind
{
    TargetStatus,
    DamageSourceStatus,
}

internal enum MitigationScopeKind
{
    Personal,
    Targeted,
    PartyWide,
    DamageSourceDebuff,
    Other,
}

internal enum MitigationEffectKind
{
    DamageReduction,
    Shield,
    Invulnerability,
    Other,
}

internal sealed record MitigationDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required uint StatusId { get; init; }

    public required MitigationApplicationKind ApplicationKind { get; init; }

    public required MitigationScopeKind ScopeKind { get; init; }

    public required MitigationEffectKind EffectKind { get; init; }

    public double? DamageReductionFraction { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (StatusId == 0)
        {
            throw new InvalidOperationException($"Mitigation definition '{Id}' must reference a non-zero status ID.");
        }

        if (ApplicationKind == MitigationApplicationKind.DamageSourceStatus &&
            ScopeKind is not MitigationScopeKind.DamageSourceDebuff and not MitigationScopeKind.Other)
        {
            throw new InvalidOperationException(
                $"Mitigation definition '{Id}' uses damage-source status evidence but declares incompatible scope {ScopeKind}.");
        }

        if (ApplicationKind == MitigationApplicationKind.TargetStatus &&
            ScopeKind == MitigationScopeKind.DamageSourceDebuff)
        {
            throw new InvalidOperationException(
                $"Mitigation definition '{Id}' declares damage-source-debuff scope but uses target-status evidence.");
        }

        if (DamageReductionFraction is { } reduction && (reduction <= 0.0 || reduction >= 1.0))
        {
            throw new InvalidOperationException(
                $"Mitigation definition '{Id}' has invalid reduction fraction {reduction}. Expected a value greater than 0 and less than 1.");
        }

        if (EffectKind == MitigationEffectKind.DamageReduction && DamageReductionFraction is null)
        {
            throw new InvalidOperationException(
                $"Damage-reduction mitigation definition '{Id}' must provide DamageReductionFraction.");
        }

        if (EffectKind != MitigationEffectKind.DamageReduction && DamageReductionFraction is not null)
        {
            throw new InvalidOperationException(
                $"Mitigation definition '{Id}' supplies a reduction fraction but its effect kind is {EffectKind}.");
        }
    }
}
