namespace BetterDeaths;

using BetterDeaths.Domain;
using System.Text.Json;

public sealed class CanonicalDomainSerializationTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 8, 18, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecordedPullRoundTripPreservesIdentityOrderingProvenanceAndTypedEvents()
    {
        var playerId = new ActorId(1);
        var petId = new ActorId(2);
        var enemyId = new ActorId(3);
        var exact = EventSource(CaptureFidelity.Exact, 1.0f);
        var sampled = EventSource(CaptureFidelity.Sampled, 0.9f);

        var pull = new RecordedPull
        {
            Id = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1234,
                TerritoryName = "Test Ultimate",
                Duration = TimeSpan.FromMinutes(17.5),
                StartedAt = StartedAt,
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord
                {
                    Id = playerId,
                    Name = "Player One",
                    Kind = ActorKind.Player,
                    ClassJobId = 38,
                    JobAbbreviation = "DNC",
                },
                new ActorRecord
                {
                    Id = petId,
                    Name = "Companion",
                    Kind = ActorKind.Pet,
                    OwnerActorId = playerId,
                },
                new ActorRecord
                {
                    Id = enemyId,
                    Name = "Boss",
                    Kind = ActorKind.Enemy,
                },
            ],
            Events =
            [
                BaseEvent(new DamageEvent { Amount = 125000, ActionId = 1001, IsCritical = true }, 1, enemyId, playerId, exact),
                BaseEvent(new HealEvent { Amount = 48000, ActionId = 2001 }, 2, playerId, playerId, exact),
                BaseEvent(new CastStartEvent { ActionId = 3001, CastDuration = TimeSpan.FromSeconds(4.2) }, 3, enemyId, null, exact),
                BaseEvent(new CastEndEvent { ActionId = 3001 }, 4, enemyId, null, exact),
                BaseEvent(new ActionUseEvent { ActionId = 4001 }, 5, playerId, enemyId, exact),
                BaseEvent(new StatusApplyEvent { StatusId = 5001, Stacks = 2, Duration = TimeSpan.FromSeconds(15) }, 6, enemyId, playerId, exact),
                BaseEvent(new StatusRemoveEvent { StatusId = 5001 }, 7, enemyId, playerId, exact),
                BaseEvent(new TargetabilityEvent { IsTargetable = false }, 8, enemyId, enemyId, exact),
                BaseEvent(new GaugeEvent { GaugeKey = "esprit", Value = 76 }, 9, playerId, playerId, EventSource(CaptureFidelity.Derived, 0.95f)),
                BaseEvent(new TetherEvent { TetherId = 84 }, 10, enemyId, playerId, exact),
                BaseEvent(new MarkerEvent { MarkerId = 17 }, 11, null, playerId, exact),
                BaseEvent(new MechanicSignalEvent { SignalKey = "arena-state", SignalId = 6001, State = 2 }, 12, enemyId, null, EventSource(CaptureFidelity.Inferred, 0.8f)),
                BaseEvent(new RaiseEvent { ActionId = 7001 }, 13, playerId, petId, exact),
                BaseEvent(new DeathEvent(), 14, enemyId, playerId, exact),
            ],
            Positions =
            [
                new PositionSample
                {
                    Sequence = 15,
                    PullTime = TimeSpan.FromSeconds(15),
                    ActorId = playerId,
                    X = 101.5f,
                    Y = 0.25f,
                    Z = 98.75f,
                    Rotation = 1.5f,
                    Provenance = sampled,
                },
            ],
            WorldMarkers =
            [
                new WorldMarkerSample
                {
                    Sequence = 16,
                    PullTime = TimeSpan.FromSeconds(16),
                    MarkerIndex = 2,
                    Label = "C",
                    Active = true,
                    X = 95.0f,
                    Y = 0.0f,
                    Z = 100.0f,
                    Provenance = sampled,
                },
            ],
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = "local-pull-fixture",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
                ProducerVersion = "0.1.0-test",
            },
        };

        var json = JsonSerializer.Serialize(pull);
        var roundTripped = JsonSerializer.Deserialize<RecordedPull>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(pull.Id, roundTripped.Id);
        Assert.Equal(new PullSchemaVersion(1), roundTripped.SchemaVersion);
        Assert.Equal(TimeSpan.FromMinutes(17.5), roundTripped.Metadata.Duration);
        Assert.Equal(StartedAt, roundTripped.Metadata.StartedAt);
        Assert.Equal(PullDataSourceKind.DalamudLive, roundTripped.Provenance.SourceKind);

        var pet = Assert.Single(roundTripped.Actors.Where(actor => actor.Kind == ActorKind.Pet));
        Assert.Equal(playerId, pet.OwnerActorId);

        Assert.Equal(14, roundTripped.Events.Count);
        Assert.Equal(Enumerable.Range(1, 14).Select(value => (long)value), roundTripped.Events.Select(evt => evt.Sequence));
        Assert.Equal(Enumerable.Range(1, 14).Select(value => new EventId(value)), roundTripped.Events.Select(evt => evt.Id));

        var damage = Assert.IsType<DamageEvent>(roundTripped.Events[0]);
        Assert.Equal(125000, damage.Amount);
        Assert.Equal(enemyId, damage.SourceActorId);
        Assert.Equal(playerId, damage.TargetActorId);
        Assert.Equal(TimeSpan.FromSeconds(1), damage.PullTime);
        Assert.Equal(CaptureFidelity.Exact, damage.Provenance.Fidelity);

        Assert.IsType<HealEvent>(roundTripped.Events[1]);
        Assert.IsType<CastStartEvent>(roundTripped.Events[2]);
        Assert.IsType<CastEndEvent>(roundTripped.Events[3]);
        Assert.IsType<ActionUseEvent>(roundTripped.Events[4]);
        Assert.IsType<StatusApplyEvent>(roundTripped.Events[5]);
        Assert.IsType<StatusRemoveEvent>(roundTripped.Events[6]);
        Assert.IsType<TargetabilityEvent>(roundTripped.Events[7]);
        Assert.IsType<GaugeEvent>(roundTripped.Events[8]);
        Assert.IsType<TetherEvent>(roundTripped.Events[9]);
        Assert.IsType<MarkerEvent>(roundTripped.Events[10]);
        Assert.IsType<MechanicSignalEvent>(roundTripped.Events[11]);
        Assert.IsType<RaiseEvent>(roundTripped.Events[12]);
        Assert.IsType<DeathEvent>(roundTripped.Events[13]);

        var position = Assert.Single(roundTripped.Positions);
        Assert.Equal(playerId, position.ActorId);
        Assert.Equal(CaptureFidelity.Sampled, position.Provenance.Fidelity);

        var worldMarker = Assert.Single(roundTripped.WorldMarkers);
        Assert.Equal(2, worldMarker.MarkerIndex);
        Assert.Equal("C", worldMarker.Label);
        Assert.True(worldMarker.Active);
    }

    [Fact]
    public void AnalysisResultRoundTripPreservesStructuredEvidenceAndConfidence()
    {
        var actorId = new ActorId(1);
        var eventIds = new[] { new EventId(12), new EventId(14) };
        var range = new TimeRange(TimeSpan.FromSeconds(11.5), TimeSpan.FromSeconds(14.25));
        var result = new AnalysisResult
        {
            Id = new AnalysisResultId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            AnalyzerId = "generic.deaths",
            Severity = AnalysisSeverity.Error,
            Category = AnalysisCategory.Death,
            Title = "Player One died",
            Summary = "Deterministic death finding for serialization coverage.",
            TimeRange = range,
            Actors = [actorId],
            Evidence =
            [
                new AnalysisEvidence
                {
                    EventIds = eventIds,
                    ActorIds = [actorId],
                    TimeRange = range,
                    Explanation = "Events 12 and 14 establish the causal window.",
                },
            ],
            Confidence = 0.98f,
            Metrics = new Dictionary<string, double>
            {
                ["damage"] = 125000,
                ["windowSeconds"] = 2.75,
            },
        };

        var json = JsonSerializer.Serialize(result);
        var roundTripped = JsonSerializer.Deserialize<AnalysisResult>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(result.Id, roundTripped.Id);
        Assert.Equal(AnalysisSeverity.Error, roundTripped.Severity);
        Assert.Equal(AnalysisCategory.Death, roundTripped.Category);
        Assert.Equal(range, roundTripped.TimeRange);
        Assert.Equal(0.98f, roundTripped.Confidence);
        Assert.Equal(actorId, Assert.Single(roundTripped.Actors));
        Assert.Equal(eventIds, Assert.Single(roundTripped.Evidence).EventIds);
        Assert.Equal(125000, roundTripped.Metrics["damage"]);
    }

    private static T BaseEvent<T>(
        T evt,
        long sequence,
        ActorId? sourceActorId,
        ActorId? targetActorId,
        EventProvenance provenance)
        where T : NormalizedEvent
    {
        return evt with
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(sequence),
            ObservedAt = StartedAt.AddSeconds(sequence),
            SourceActorId = sourceActorId,
            TargetActorId = targetActorId,
            Provenance = provenance,
        };
    }

    private static EventProvenance EventSource(CaptureFidelity fidelity, float confidence)
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = "local-pull-fixture",
            Fidelity = fidelity,
            Confidence = confidence,
        };
    }
}
