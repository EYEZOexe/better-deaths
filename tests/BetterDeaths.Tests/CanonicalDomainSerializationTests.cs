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
                new DamageEvent
                {
                    Id = new EventId(1),
                    Sequence = 1,
                    PullTime = TimeSpan.FromSeconds(1),
                    ObservedAt = StartedAt.AddSeconds(1),
                    SourceActorId = enemyId,
                    TargetActorId = playerId,
                    Provenance = exact,
                    Amount = 125000,
                    ActionId = 1001,
                    IsCritical = true,
                },
                new HealEvent
                {
                    Id = new EventId(2),
                    Sequence = 2,
                    PullTime = TimeSpan.FromSeconds(2),
                    ObservedAt = StartedAt.AddSeconds(2),
                    SourceActorId = playerId,
                    TargetActorId = playerId,
                    Provenance = exact,
                    Amount = 48000,
                    ActionId = 2001,
                },
                new CastStartEvent
                {
                    Id = new EventId(3),
                    Sequence = 3,
                    PullTime = TimeSpan.FromSeconds(3),
                    ObservedAt = StartedAt.AddSeconds(3),
                    SourceActorId = enemyId,
                    Provenance = exact,
                    ActionId = 3001,
                    CastDuration = TimeSpan.FromSeconds(4.2),
                },
                new CastEndEvent
                {
                    Id = new EventId(4),
                    Sequence = 4,
                    PullTime = TimeSpan.FromSeconds(4),
                    ObservedAt = StartedAt.AddSeconds(4),
                    SourceActorId = enemyId,
                    Provenance = exact,
                    ActionId = 3001,
                },
                new ActionUseEvent
                {
                    Id = new EventId(5),
                    Sequence = 5,
                    PullTime = TimeSpan.FromSeconds(5),
                    ObservedAt = StartedAt.AddSeconds(5),
                    SourceActorId = playerId,
                    TargetActorId = enemyId,
                    Provenance = exact,
                    ActionId = 4001,
                },
                new StatusApplyEvent
                {
                    Id = new EventId(6),
                    Sequence = 6,
                    PullTime = TimeSpan.FromSeconds(6),
                    ObservedAt = StartedAt.AddSeconds(6),
                    SourceActorId = enemyId,
                    TargetActorId = playerId,
                    Provenance = exact,
                    StatusId = 5001,
                    Stacks = 2,
                    Duration = TimeSpan.FromSeconds(15),
                },
                new StatusRemoveEvent
                {
                    Id = new EventId(7),
                    Sequence = 7,
                    PullTime = TimeSpan.FromSeconds(7),
                    ObservedAt = StartedAt.AddSeconds(7),
                    SourceActorId = enemyId,
                    TargetActorId = playerId,
                    Provenance = exact,
                    StatusId = 5001,
                },
                new TargetabilityEvent
                {
                    Id = new EventId(8),
                    Sequence = 8,
                    PullTime = TimeSpan.FromSeconds(8),
                    ObservedAt = StartedAt.AddSeconds(8),
                    SourceActorId = enemyId,
                    TargetActorId = enemyId,
                    Provenance = exact,
                    IsTargetable = false,
                },
                new GaugeEvent
                {
                    Id = new EventId(9),
                    Sequence = 9,
                    PullTime = TimeSpan.FromSeconds(9),
                    ObservedAt = StartedAt.AddSeconds(9),
                    SourceActorId = playerId,
                    TargetActorId = playerId,
                    Provenance = EventSource(CaptureFidelity.Derived, 0.95f),
                    GaugeKey = "esprit",
                    Value = 76,
                },
                new TetherEvent
                {
                    Id = new EventId(10),
                    Sequence = 10,
                    PullTime = TimeSpan.FromSeconds(10),
                    ObservedAt = StartedAt.AddSeconds(10),
                    SourceActorId = enemyId,
                    TargetActorId = playerId,
                    Provenance = exact,
                    TetherId = 84,
                },
                new MarkerEvent
                {
                    Id = new EventId(11),
                    Sequence = 11,
                    PullTime = TimeSpan.FromSeconds(11),
                    ObservedAt = StartedAt.AddSeconds(11),
                    TargetActorId = playerId,
                    Provenance = exact,
                    MarkerId = 17,
                },
                new MechanicSignalEvent
                {
                    Id = new EventId(12),
                    Sequence = 12,
                    PullTime = TimeSpan.FromSeconds(12),
                    ObservedAt = StartedAt.AddSeconds(12),
                    SourceActorId = enemyId,
                    Provenance = EventSource(CaptureFidelity.Inferred, 0.8f),
                    SignalKey = "arena-state",
                    SignalId = 6001,
                    State = 2,
                },
                new RaiseEvent
                {
                    Id = new EventId(13),
                    Sequence = 13,
                    PullTime = TimeSpan.FromSeconds(13),
                    ObservedAt = StartedAt.AddSeconds(13),
                    SourceActorId = playerId,
                    TargetActorId = petId,
                    Provenance = exact,
                    ActionId = 7001,
                },
                new DeathEvent
                {
                    Id = new EventId(14),
                    Sequence = 14,
                    PullTime = TimeSpan.FromSeconds(14),
                    ObservedAt = StartedAt.AddSeconds(14),
                    SourceActorId = enemyId,
                    TargetActorId = playerId,
                    Provenance = exact,
                },
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

        var pet = Assert.Single(roundTripped.Actors, actor => actor.Kind == ActorKind.Pet);
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
        Assert.Equal(125000d, roundTripped.Metrics["damage"]);
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
