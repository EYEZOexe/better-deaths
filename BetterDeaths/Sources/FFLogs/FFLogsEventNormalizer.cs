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
        var skipped = new List<FFLogsSkippedEvent>();
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
                    actorDirectory.SourceToCanonical,
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
        };
    }

    private static bool TryTranslate(
        FFLogsEventEnvelope envelope,
        int inputIndex,
        double fightStartMilliseconds,
        double reportStartUnixMilliseconds,
        string sourceReference,
        IReadOnlyDictionary<int, ActorId> actorIds,
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
        var source = TryResolveActor(payload, "sourceID", actorIds);
        var target = TryResolveActor(payload, "targetID", actorIds);
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
                        ActionId = actionId,
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
                        ActionId = actionId,
                    }));
                return true;

            case "begincast":
                if (actionId is null || TryGetDouble(payload, "duration") is not { } castDurationMilliseconds)
                {
                    reason = "cast-start event lacks action or duration evidence";
                    return false;
                }

                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new CastStartEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                        ActionId = actionId.Value,
                        CastDuration = TimeSpan.FromMilliseconds(Math.Max(0.0, castDurationMilliseconds)),
                    }));
                return true;

            case "cast":
                if (actionId is null)
                {
                    reason = "cast event missing action identity";
                    return false;
                }

                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new ActionUseEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                        ActionId = actionId.Value,
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
                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new StatusApplyEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                        StatusId = actionId.Value,
                        Stacks = stacks > ushort.MaxValue ? ushort.MaxValue : (ushort)stacks,
                        Duration = durationMilliseconds is { } duration
                            ? TimeSpan.FromMilliseconds(Math.Max(0.0, duration))
                            : null,
                    }));
                return true;

            case "removebuff":
            case "removedebuff":
                if (actionId is null)
                {
                    reason = "status removal missing status identity";
                    return false;
                }

                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new StatusRemoveEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                        StatusId = actionId.Value,
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

                translated = new TranslatedEvent(
                    inputIndex,
                    pullTime,
                    (id, sequence) => CreateBase(new RaiseEvent
                    {
                        Id = id,
                        Sequence = sequence,
                        PullTime = pullTime,
                        Provenance = provenance,
                        ActionId = actionId,
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

        var referenced = new SortedSet<int>(metadataBySourceId.Keys);
        foreach (var evt in events)
        {
            AddReferencedActor(evt.Payload, "sourceID", referenced);
            AddReferencedActor(evt.Payload, "targetID", referenced);
        }

        var sourceToCanonical = new Dictionary<int, ActorId>();
        var nextActorId = 1;
        foreach (var sourceId in referenced)
        {
            sourceToCanonical.Add(sourceId, new ActorId(nextActorId++));
        }

        var actors = new List<ActorRecord>(referenced.Count);
        foreach (var sourceId in referenced)
        {
            metadataBySourceId.TryGetValue(sourceId, out var metadata);
            ActorId? owner = null;
            if (metadata?.PetOwnerId is { } ownerSourceId && sourceToCanonical.TryGetValue(ownerSourceId, out var ownerId))
            {
                owner = ownerId;
            }

            actors.Add(new ActorRecord
            {
                Id = sourceToCanonical[sourceId],
                Name = string.IsNullOrWhiteSpace(metadata?.Name)
                    ? $"FFLogs Actor {sourceId.ToString(CultureInfo.InvariantCulture)}"
                    : metadata!.Name.Trim(),
                Kind = MapActorKind(metadata?.Type, metadata?.PetOwnerId),
                JobAbbreviation = string.Equals(metadata?.Type, "Player", StringComparison.OrdinalIgnoreCase) &&
                                  !string.IsNullOrWhiteSpace(metadata?.SubType)
                    ? metadata.SubType.Trim()
                    : null,
                OwnerActorId = owner,
            });
        }

        return new ActorDirectory(actors, sourceToCanonical);
    }

    private static ActorKind MapActorKind(string? type, int? ownerSourceId)
    {
        if (ownerSourceId is not null)
        {
            return ActorKind.Pet;
        }

        return type?.Trim().ToLowerInvariant() switch
        {
            "player" => ActorKind.Player,
            "pet" => ActorKind.Pet,
            "npc" => ActorKind.Npc,
            "boss" => ActorKind.Enemy,
            "enemy" => ActorKind.Enemy,
            "object" => ActorKind.Object,
            _ => ActorKind.Unknown,
        };
    }

    private static string? TryGetExplicitSourceIdentity(FFLogsEventEnvelope envelope)
    {
        foreach (var key in new[] { "eventID", "packetID" })
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
        string propertyName,
        IReadOnlyDictionary<int, ActorId> actorIds)
    {
        var sourceId = TryGetInt32(payload, propertyName);
        return sourceId is { } id && actorIds.TryGetValue(id, out var actorId)
            ? actorId
            : null;
    }

    private static void AddReferencedActor(JsonElement payload, string propertyName, ISet<int> referenced)
    {
        if (TryGetInt32(payload, propertyName) is > 0 and var sourceId)
        {
            referenced.Add(sourceId);
        }
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

    private sealed record ActorDirectory(
        IReadOnlyList<ActorRecord> Actors,
        IReadOnlyDictionary<int, ActorId> SourceToCanonical);

    private sealed record TranslatedEvent(
        int InputIndex,
        TimeSpan PullTime,
        Func<EventId, long, NormalizedEvent> Factory);
}
