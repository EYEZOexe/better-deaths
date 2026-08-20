namespace BetterDeaths.Sources.FFLogs;

using System;
using System.Collections.Generic;

internal enum FFLogsAbilityEventCategory
{
    Action,
    Status,
}

internal enum FFLogsAbilityIdentityClassification
{
    CataloguedSourceIdentity,
    VerifiedStatusMapping,
    UncataloguedPreserved,
}

internal sealed record FFLogsAbilityIdentityResolution(
    uint SourceId,
    uint CanonicalId,
    FFLogsAbilityIdentityClassification Classification,
    string? DiagnosticReason);

internal sealed record FFLogsAbilityIdentityDiagnostic(
    int InputIndex,
    double TimestampMilliseconds,
    string EventType,
    uint SourceId,
    FFLogsAbilityIdentityClassification Classification,
    string Reason);

internal enum FFLogsStatusDurationClassification
{
    Missing,
    Preserved,
    NegativeUnavailable,
    IndefiniteSentinelUnavailable,
    OutOfRangeUnavailable,
}

internal sealed record FFLogsStatusDurationResolution(
    TimeSpan? Duration,
    FFLogsStatusDurationClassification Classification,
    string? DiagnosticReason);

internal sealed record FFLogsStatusDurationDiagnostic(
    int InputIndex,
    double TimestampMilliseconds,
    string EventType,
    double SourceDurationMilliseconds,
    FFLogsStatusDurationClassification Classification,
    string Reason);

internal sealed class FFLogsAbilityIdentityDecoder
{
    private const double IndefiniteStatusDurationMilliseconds = 9_999_000.0;

    // Exact FFLogs status spellings evidenced by the approved M11 report baseline and authorized
    // by M12-B. Catalog presence gates every mapping; this is intentionally not an arithmetic rule.
    private static readonly IReadOnlyDictionary<uint, uint> VerifiedStatusMappings =
        new Dictionary<uint, uint>
        {
            [1_001_825] = 1_825,
            [1_005_084] = 5_084,
            [1_005_085] = 5_085,
            [1_005_086] = 5_086,
        };

    private readonly IReadOnlyDictionary<uint, FFLogsReportAbility> catalog;

    public FFLogsAbilityIdentityDecoder(IReadOnlyList<FFLogsReportAbility> abilities)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        var indexed = new Dictionary<uint, FFLogsReportAbility>();
        foreach (var ability in abilities)
        {
            ArgumentNullException.ThrowIfNull(ability);
            if (indexed.TryGetValue(ability.GameId, out var existing))
            {
                if (existing != ability)
                {
                    throw new InvalidOperationException(
                        $"FFLogs masterData contains conflicting ability metadata for game ID {ability.GameId}.");
                }

                continue;
            }

            indexed.Add(ability.GameId, ability);
        }

        catalog = indexed;
    }

    public FFLogsAbilityIdentityResolution Resolve(uint sourceId, FFLogsAbilityEventCategory category)
    {
        if (category == FFLogsAbilityEventCategory.Status &&
            catalog.ContainsKey(sourceId) &&
            VerifiedStatusMappings.TryGetValue(sourceId, out var canonicalId))
        {
            return new FFLogsAbilityIdentityResolution(
                sourceId,
                canonicalId,
                FFLogsAbilityIdentityClassification.VerifiedStatusMapping,
                DiagnosticReason: null);
        }

        if (catalog.ContainsKey(sourceId))
        {
            return new FFLogsAbilityIdentityResolution(
                sourceId,
                sourceId,
                FFLogsAbilityIdentityClassification.CataloguedSourceIdentity,
                "Catalogued ability identity has no verified canonical mapping and was preserved unchanged.");
        }

        return new FFLogsAbilityIdentityResolution(
            sourceId,
            sourceId,
            FFLogsAbilityIdentityClassification.UncataloguedPreserved,
            "Ability identity was not present in the report masterData catalog and was preserved unchanged.");
    }

    public FFLogsStatusDurationResolution ResolveStatusDuration(double? durationMilliseconds)
    {
        if (durationMilliseconds is null)
        {
            return new FFLogsStatusDurationResolution(
                Duration: null,
                FFLogsStatusDurationClassification.Missing,
                DiagnosticReason: null);
        }

        if (durationMilliseconds.Value < 0.0)
        {
            return new FFLogsStatusDurationResolution(
                Duration: null,
                FFLogsStatusDurationClassification.NegativeUnavailable,
                "Negative FFLogs status duration was treated as unavailable.");
        }

        if (durationMilliseconds.Value == IndefiniteStatusDurationMilliseconds)
        {
            return new FFLogsStatusDurationResolution(
                Duration: null,
                FFLogsStatusDurationClassification.IndefiniteSentinelUnavailable,
                "The exact FFLogs indefinite sentinel status duration was treated as unavailable.");
        }

        if (!double.IsFinite(durationMilliseconds.Value) ||
            durationMilliseconds.Value > TimeSpan.MaxValue.TotalMilliseconds)
        {
            return new FFLogsStatusDurationResolution(
                Duration: null,
                FFLogsStatusDurationClassification.OutOfRangeUnavailable,
                "FFLogs status duration was outside the canonical TimeSpan range and was treated as unavailable.");
        }

        return new FFLogsStatusDurationResolution(
            TimeSpan.FromMilliseconds(durationMilliseconds.Value),
            FFLogsStatusDurationClassification.Preserved,
            DiagnosticReason: null);
    }
}
