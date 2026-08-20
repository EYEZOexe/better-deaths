namespace BetterDeaths.Sources.FFLogs;

using BetterDeaths.Domain;
using BetterDeaths.Sources.FFLogs.Client;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal sealed record FFLogsSkippedEvent(
    int InputIndex,
    double TimestampMilliseconds,
    string Type,
    string Reason);

internal sealed record FFLogsNormalizationResult
{
    public required RecordedPull Pull { get; init; }

    public required IReadOnlyList<FFLogsSkippedEvent> SkippedEvents { get; init; }

    public required IReadOnlyList<FFLogsAbilityIdentityDiagnostic> AbilityIdentityDiagnostics { get; init; }

    public required IReadOnlyList<FFLogsStatusDurationDiagnostic> StatusDurationDiagnostics { get; init; }
}

internal static class FFLogsEventNormalizer
{
    public static FFLogsNormalizationResult Normalize(
        FFLogsFightImportData importData,
        PullSchemaVersion schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(importData);
        ArgumentNullException.ThrowIfNull(importData.ReportDocument);
        ArgumentNullException.ThrowIfNull(importData.ReportDocument.Report);
        ArgumentNullException.ThrowIfNull(importData.Fight);
        ArgumentNullException.ThrowIfNull(importData.Events);
        ArgumentNullException.ThrowIfNull(importData.Actors);
        ArgumentNullException.ThrowIfNull(importData.ReportDocument.Abilities);

        var report = importData.ReportDocument.Report;
        var fight = importData.Fight;
        FFLogsSourceReference.Validate(report.Code, fight.Id);
        ValidateFinite(report.StartTimeUnixMilliseconds, nameof(report.StartTimeUnixMilliseconds));
        ValidateFinite(fight.StartTimeMilliseconds, nameof(fight.StartTimeMilliseconds));
        ValidateFinite(fight.EndTimeMilliseconds, nameof(fight.EndTimeMilliseconds));
        if (fight.EndTimeMilliseconds < fight.StartTimeMilliseconds)
        {
            throw new InvalidOperationException("FFLogs fight end time precedes its start time.");
        }

        var sourceReference = FFLogsSourceReference.Create(report.Code, fight.Id);
        var actorDirectory = BuildActorDirectory(importData.Actors, importData.Events);
        var abilityDecoder = new FFLogsAbilityIdentityDecoder(importData.ReportDocument.Abilities);
        var skipped = new List<FFLogsSkippedEvent>();
        var abilityIdentityDiagnostics = new List<FFLogsAbilityIdentityDiagnostic>();
        var statusDurationDiagnostics = new List<FFLogsStatusDurationDiagnostic>();
        var translated = new List<TranslatedEvent>();
        var explicitSourceIdentities = new HashSet<string>(StringComparer.Ordinal);

        for (var inputIndex = 0; inputIndex < importData.Events.Count; inputIndex++)
        {
            var envelope = importData.Events[inputIndex] ?? throw new InvalidOperationException(
                "FFLogs event collections cannot contain null entries.");
            ValidateFinite(envelope.TimestampMilliseconds, $"event[{inputIndex}].timestamp");

            if (envelope.TimestampMilliseconds < fight.StartTimeMilliseconds ||
                envelope.TimestampMilliseconds > fight.EndTimeMilliseconds)
            {
                skipped.Add(new FFLogsSkippedEvent(
                    inputIndex,
                    envelope.TimestampMilliseconds,
                    envelope.Type,
                    "outside selected fight time range"));
                continue;
            }

            var explicitIdentity = TryGetExplicitSourceIdentity(envelope);
            if (explicitIdentity is not null && !explicitSourceIdentities.Add(explicitIdentity))
            {
                skipped.Add(new FFLogsSkippedEvent(
                    inputIndex,
                    envelope.TimestampMilliseconds,
                    envelope.Type,
                    "duplicate explicit source event identity"));
                continue;
            }

            if (!TryTranslate(
                    envelope,
                    inputIndex,
                    fight.StartTimeMilliseconds,
                    report.StartTimeUnixMilliseconds,
                    sourceReference,
                    actorDirectory,
                    abilityDecoder,
                    abilityIdentityDiagnostics,
                    statusDurationDiagnostics,
                    out var candidate,
                    out var reason))
            {
                skipped.Add(new FFLogsSkippedEvent(
                    inputIndex,
                    envelope.TimestampMilliseconds,
                    envelope.Type,
                    reason));
                continue;
            }

            translated.Add(candidate!);
        }

        var ordered = translated
            .OrderBy(item => item.PullTime)
            .ThenBy(item => item.InputIndex)
            .ToArray();
        var events = new NormalizedEvent[ordered.Length];
        for (var index = 0; index < ordered.Length; index++)
        {
            var sequence = index + 1L;
            events[index] = ordered[index].Factory(new EventId(sequence), sequence);
        }

        var duration = TimeSpan.FromMilliseconds(fight.EndTimeMilliseconds - fight.StartTimeMilliseconds);
        var startedAt = DateTimeOffset.FromUnixTimeMilliseconds(checked((long)Math.Round(report.StartTimeUnixMilliseconds)))
            + TimeSpan.FromMilliseconds(fight.StartTimeMilliseconds);
        var territoryId = NormalizeTerritoryId(fight.GameZoneId);
        var pullId = CreateStablePullId(report.Code, fight.Id, report.Revision);

        return new FFLogsNormalizationResult
        {
            Pull = new RecordedPull
            {
                Id = pullId,
                Metadata = new PullMetadata
                {
                    TerritoryId = territoryId,
                    TerritoryName = string.IsNullOrWhiteSpace(fight.GameZoneName)
                        ? fight.Name
                        : fight.GameZoneName.Trim(),
                    Duration = duration,
                    StartedAt = startedAt,
                },
                SchemaVersion = schemaVersion,
                Actors = actorDirectory.Actors,
                Events = events,
                Provenance = new PullProvenance
                {
                    SourceKind = PullDataSourceKind.FFLogs,
                    SourceReference = sourceReference,
                    Fidelity = CaptureFidelity.Exact,
                    Confidence = 1.0f,
                },
            },
            SkippedEvents = skipped,
            AbilityIdentityDiagnostics = abilityIdentityDiagnostics,
            StatusDurationDiagnostics = statusDurationDiagnostics,
        };
    }

    private static bool TryTranslate(
        FFLogsEventEnvelope envelope,
        int inputIndex,
        double fightStartMilliseconds,
        double reportStartUnixMilliseconds,
        string sourceReference,
        ActorDirectory actorDirectory,
        FFLogsAbilityIdentityDecoder abilityDecoder,
        ICollection<FFLogsAbilityIdentityDiagnostic> abilityIdentityDiagnostics,
        ICollection<FFLogsStatusDurationDiagnostic> statusDurationDiagnostics,
        out TranslatedEvent? translated,
        out string reason)
    {
        translated = null;
        reason = string.Empty;
        var normalizedType = envelope.Type?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedType))
        {
            reason = "missing event type";
            return false;
        }

        var payload = envelope.Payload;
        var source = TryResolveActor(payload, "source", actorDirectory);
        var target = TryResolveActor(payload, "target", actorDirectory);
        var actionId = TryGetUInt32(payload, "abilityGameID") ?? TryGetNestedAbilityId(payload);
        var pullTime = TimeSpan.FromMilliseconds(envelope.TimestampMilliseconds - fightStartMilliseconds);
        var observedAt = DateTimeOffset.FromUnixTimeMilliseconds(checked((long)Math.Round(reportStartUnixMilliseconds)))
            + TimeSpan.FromMilliseconds(envelope.TimestampMilliseconds);
        var provenance = new EventProvenance
        {
            SourceKind = PullDataSourceKind.FFLogs,
            SourceReference = sourceReference,
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };

        uint? ResolveAbility(FFLogsAbilityEventCategory category)
        {
            if (actionId is null)
            {
                return null;
            }

            var resolution = abilityDecoder.Resolve(actionId.Value, category);
            if (resolution.DiagnosticReason is { } diagnosticReason)
            {
                abilityIdentityDiagnostics.Add(new FFLogsAbilityIdentityDiagnostic(
                    inputIndex,
                    envelope.TimestampMilliseconds,
                    envelope.Type ?? string.Empty,
                    resolution.SourceId,
                    resolution.Classification,
                    diagnosticReason));
            }

            return resolution.CanonicalId;
        }

        TimeSpan? ResolveStatusDuration(double? sourceDurationMilliseconds)
        {
            var resolution = abilityDecoder.ResolveStatusDuration(sourceDurationMilliseconds);
            if (sourceDurationMilliseconds is { } sourceDuration &&
                resolution.DiagnosticReason is { } diagnosticReason)
            {
                statusDurationDiagnostics.Add(new FFLogsStatusDurationDiagnostic(
                    inputIndex,
                    envelope.TimestampMilliseconds,
                    envelope.Type ?? string.Empty,
                    sourceDuration,
                    resolution.Classification,
                    diagnosticReason));
            }

            return resolution.Duration;
        }

        NormalizedEvent CreateBase(NormalizedEvent evt)
        {
            return evt with
            {
                PullTime = pullTime,
                ObservedAt = observedAt,
                SourceActorId = source,
                TargetActorId = target,
                Provenance = provenance,
            };
        }

        switch (normalizedType)
        {
            case "damage":
            case "calculateddamage":
                if (TryGetInt64(payload, "amount") is not { } damageAmount)
                {
                    reason = "damage event missing amount";
                    return false;
                }

                var damageActionId = ResolveAbility(FFLogsAbilityEventCategory.Action);
                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new DamageEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                        Amount = damageAmount,
                        ActionId = damageActionId,
                        IsCritical = TryGetBoolean(payload, "critical") ?? false,
                        IsDirectHit = TryGetBoolean(payload, "directHit") ?? false,
                    }));
                return true;

            case "heal":
            case "calculatedheal":
                if (TryGetInt64(payload, "amount") is not { } healAmount)
                {
                    reason = "heal event missing amount";
                    return false;
                }

                var healActionId = ResolveAbility(FFLogsAbilityEventCategory.Action);
                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new HealEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                        Amount = healAmount,
                        ActionId = healActionId,
                    }));
                return true;

            case "begincast":
                if (actionId is null || TryGetDouble(payload, "duration") is not { } castDurationMilliseconds)
                {
                    reason = "cast-start event lacks action or duration evidence";
                    return false;
                }

                var castStartActionId = ResolveAbility(FFLogsAbilityEventCategory.Action)!.Value;
                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new CastStartEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                        ActionId = castStartActionId,
                        CastDuration = TimeSpan.FromMilliseconds(Math.Max(0.0, castDurationMilliseconds)),
                    }));
                return true;

            case "cast":
                if (actionId is null)
                {
                    reason = "cast event missing action identity";
                    return false;
                }

                var actionUseId = ResolveAbility(FFLogsAbilityEventCategory.Action)!.Value;
                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new ActionUseEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                        ActionId = actionUseId,
                    }));
                return true;

            case "applybuff":
            case "applydebuff":
            case "refreshbuff":
            case "refreshdebuff":
                if (actionId is null)
                {
                    reason = "status application missing status identity";
                    return false;
                }

                var durationMilliseconds = TryGetDouble(payload, "duration");
                var stacks = TryGetUInt32(payload, "stack") ?? TryGetUInt32(payload, "stacks") ?? 0;
                var appliedStatusId = ResolveAbility(FFLogsAbilityEventCategory.Status)!.Value;
                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new StatusApplyEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                        StatusId = appliedStatusId,
                        Stacks = stacks > ushort.MaxValue ? ushort.MaxValue : (ushort)stacks,
                        Duration = ResolveStatusDuration(durationMilliseconds),
                    }));
                return true;

            case "removebuff":
            case "removedebuff":
                if (actionId is null)
                {
                    reason = "status removal missing status identity";
                    return false;
                }

                var removedStatusId = ResolveAbility(FFLogsAbilityEventCategory.Status)!.Value;
                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new StatusRemoveEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                        StatusId = removedStatusId,
                    }));
                return true;

            case "death":
                if (target is null)
                {
                    reason = "death event missing target actor";
                    return false;
                }

                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new DeathEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                    }));
                return true;

            case "resurrect":
                if (target is null)
                {
                    reason = "resurrect event missing target actor";
                    return false;
                }

                var raiseActionId = ResolveAbility(FFLogsAbilityEventCategory.Action);
                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new RaiseEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                        ActionId = raiseActionId,
                    }));
                return true;

            case "targetabilityupdate":
                if (target is null ||
                    (TryGetBoolean(payload, "targetable") ?? TryGetBoolean(payload, "isTargetable")) is not { } isTargetable)
                {
                    reason = "targetability event missing target or targetable state";
                    return false;
                }

                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new TargetabilityEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                        IsTargetable = isTargetable,
                    }));
                return true;

            default:
                reason = $"unsupported FFLogs event type '{normalizedType}'";
                return false;
        }
    }

    private static ActorDirectory BuildActorDirectory(
        IReadOnlyList<FFLogsReportActor> sourceActors,
        IReadOnlyList<FFLogsEventEnvelope> events)
    {
        var metadataBySourceId = new Dictionary<int, FFLogsReportActor>();
        foreach (var actor in sourceActors.OrderBy(actor => actor.Id))
        {
            if (actor.Id <= 0)
            {
                throw new InvalidOperationException("FFLogs actor IDs must be positive.");
            }

            if (!metadataBySourceId.TryAdd(actor.Id, actor))
            {
                throw new InvalidOperationException($"Duplicate FFLogs actor ID {actor.Id}.");
            }
        }

        var referenced = new SortedSet<SourceActorKey>(SourceActorKeyComparer.Instance);
        foreach (var evt in events)
        {
            AddReferencedActor(evt.Payload, "source", metadataBySourceId, referenced);
            AddReferencedActor(evt.Payload, "target", metadataBySourceId, referenced);
        }

        var referencedSnapshot = referenced.ToArray();
        foreach (var actorKey in referencedSnapshot)
        {
            if (!metadataBySourceId.TryGetValue(actorKey.ReportActorId, out var metadata) ||
                metadata.PetOwnerId is not { } ownerSourceId ||
                ownerSourceId <= 0)
            {
                continue;
            }

            referenced.Add(new SourceActorKey(ownerSourceId, null));
        }

        var sourceToCanonical = new Dictionary<SourceActorKey, ActorId>();
        var nextActorId = 1;
        foreach (var sourceKey in referenced)
        {
            sourceToCanonical.Add(sourceKey, new ActorId(nextActorId++));
        }

        var actors = new List<ActorRecord>(referenced.Count);
        foreach (var sourceKey in referenced)
        {
            metadataBySourceId.TryGetValue(sourceKey.ReportActorId, out var metadata);
            ActorId? owner = null;
            if (metadata?.PetOwnerId is { } ownerSourceId &&
                ownerSourceId > 0 &&
                sourceToCanonical.TryGetValue(new SourceActorKey(ownerSourceId, null), out var ownerId))
            {
                owner = ownerId;
            }

            var kind = MapActorKind(metadata?.Type, metadata?.SubType, metadata?.PetOwnerId);
            actors.Add(new ActorRecord
            {
                Id = sourceToCanonical[sourceKey],
                Name = BuildActorName(metadata, sourceKey),
                Kind = kind,
                JobAbbreviation = FFLogsJobIdentityMapper.ToCanonicalAbbreviation(
                    kind,
                    metadata?.SubType),
                OwnerActorId = owner,
            });
        }

        return new ActorDirectory(actors, sourceToCanonical, metadataBySourceId);
    }

    private static string BuildActorName(FFLogsReportActor? metadata, SourceActorKey sourceKey)
    {
        if (!string.IsNullOrWhiteSpace(metadata?.Name))
        {
            return metadata.Name.Trim();
        }

        var instanceSuffix = sourceKey.InstanceId is { } instanceId && instanceId > 0
            ? $" instance {instanceId.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;
        return $"FFLogs Actor {sourceKey.ReportActorId.ToString(CultureInfo.InvariantCulture)}{instanceSuffix}";
    }

    private static ActorKind MapActorKind(string? type, string? subType, int? ownerSourceId)
    {
        if (ownerSourceId is not null || string.Equals(type, "Pet", StringComparison.OrdinalIgnoreCase))
        {
            return ActorKind.Pet;
        }

        if (string.Equals(type, "Player", StringComparison.OrdinalIgnoreCase))
        {
            return ActorKind.Player;
        }

        if (string.Equals(type, "NPC", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(subType, "Boss", StringComparison.OrdinalIgnoreCase)
                ? ActorKind.Enemy
                : ActorKind.Npc;
        }

        return type?.Trim().ToLowerInvariant() switch
        {
            "boss" => ActorKind.Enemy,
            "enemy" => ActorKind.Enemy,
            "object" => ActorKind.Object,
            _ => ActorKind.Unknown,
        };
    }

    private static string? TryGetExplicitSourceIdentity(FFLogsEventEnvelope envelope)
    {
        foreach (var key in new[] { "eventID", "eventId", "packetID", "packetId" })
        {
            if (envelope.Payload.ValueKind == JsonValueKind.Object &&
                envelope.Payload.TryGetProperty(key, out var property) &&
                property.ValueKind is JsonValueKind.Number or JsonValueKind.String)
            {
                return $"{envelope.Type.Trim().ToLowerInvariant()}:{property.GetRawText()}";
            }
        }

        return null;
    }

    private static ActorId? TryResolveActor(
        JsonElement payload,
        string actorPrefix,
        ActorDirectory actorDirectory)
    {
        var key = TryGetActorKey(payload, actorPrefix, actorDirectory.MetadataBySourceId);
        return key is { } sourceKey && actorDirectory.SourceToCanonical.TryGetValue(sourceKey, out var actorId)
            ? actorId
            : null;
    }

    private static void AddReferencedActor(
        JsonElement payload,
        string actorPrefix,
        IReadOnlyDictionary<int, FFLogsReportActor> metadataBySourceId,
        ISet<SourceActorKey> referenced)
    {
        if (TryGetActorKey(payload, actorPrefix, metadataBySourceId) is { } key)
        {
            referenced.Add(key);
        }
    }

    private static SourceActorKey? TryGetActorKey(
        JsonElement payload,
        string actorPrefix,
        IReadOnlyDictionary<int, FFLogsReportActor> metadataBySourceId)
    {
        var sourceId = TryGetInt32(payload, $"{actorPrefix}ID");
        if (sourceId is not { } actorId || actorId <= 0)
        {
            return null;
        }

        if (metadataBySourceId.TryGetValue(actorId, out var metadata) &&
            string.Equals(metadata.Type, "Player", StringComparison.OrdinalIgnoreCase))
        {
            return new SourceActorKey(actorId, null);
        }

        return new SourceActorKey(actorId, TryGetActorInstanceId(payload, actorPrefix));
    }

    private static int? TryGetActorInstanceId(JsonElement payload, string actorPrefix)
    {
        foreach (var suffix in new[] { "InstanceID", "InstanceId", "Instance" })
        {
            if (TryGetInt32(payload, $"{actorPrefix}{suffix}") is { } instanceId && instanceId > 0)
            {
                return instanceId;
            }
        }

        return null;
    }

    private static int? TryGetInt32(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var numeric))
        {
            return numeric;
        }

        return property.ValueKind == JsonValueKind.String &&
               int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric)
            ? numeric
            : null;
    }

    private static uint? TryGetUInt32(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetUInt32(out var numeric))
        {
            return numeric;
        }

        return property.ValueKind == JsonValueKind.String &&
               uint.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric)
            ? numeric
            : null;
    }

    private static long? TryGetInt64(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var numeric))
        {
            return numeric;
        }

        return property.ValueKind == JsonValueKind.String &&
               long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric)
            ? numeric
            : null;
    }

    private static double? TryGetDouble(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var numeric) && double.IsFinite(numeric))
        {
            return numeric;
        }

        return property.ValueKind == JsonValueKind.String &&
               double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out numeric) &&
               double.IsFinite(numeric)
            ? numeric
            : null;
    }

    private static bool? TryGetBoolean(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when property.TryGetInt32(out var number) => number != 0,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var boolean) => boolean,
            _ => null,
        };
    }

    private static uint? TryGetNestedAbilityId(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("ability", out var ability) ||
            ability.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return TryGetUInt32(ability, "guid") ?? TryGetUInt32(ability, "gameID");
    }

    private static uint NormalizeTerritoryId(double? zoneId)
    {
        if (zoneId is not { } value ||
            !double.IsFinite(value) ||
            value < 0 ||
            value > uint.MaxValue ||
            Math.Abs(value - Math.Round(value)) > double.Epsilon)
        {
            return 0;
        }

        return (uint)Math.Round(value);
    }

    private static PullId CreateStablePullId(string reportCode, int fightId, int revision)
    {
        var identity = $"fflogs|{reportCode.Trim()}|{fightId.ToString(CultureInfo.InvariantCulture)}|{revision.ToString(CultureInfo.InvariantCulture)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new PullId(new Guid(hash.AsSpan(0, 16)));
    }

    private static void ValidateFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new InvalidOperationException($"FFLogs {name} must be finite.");
        }
    }

    private readonly record struct SourceActorKey(int ReportActorId, int? InstanceId);

    private sealed class SourceActorKeyComparer : IComparer<SourceActorKey>
    {
        public static SourceActorKeyComparer Instance { get; } = new();

        public int Compare(SourceActorKey x, SourceActorKey y)
        {
            var sourceComparison = x.ReportActorId.CompareTo(y.ReportActorId);
            if (sourceComparison != 0)
            {
                return sourceComparison;
            }

            if (x.InstanceId == y.InstanceId)
            {
                return 0;
            }

            if (x.InstanceId is null)
            {
                return -1;
            }

            if (y.InstanceId is null)
            {
                return 1;
            }

            return x.InstanceId.Value.CompareTo(y.InstanceId.Value);
        }
    }

    private sealed record ActorDirectory(
        IReadOnlyList<ActorRecord> Actors,
        IReadOnlyDictionary<SourceActorKey, ActorId> SourceToCanonical,
        IReadOnlyDictionary<int, FFLogsReportActor> MetadataBySourceId);

    private sealed record TranslatedEvent(
        int InputIndex,
        TimeSpan PullTime,
        Func<EventId, long, NormalizedEvent> Factory);
}
