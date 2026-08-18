namespace BetterDeaths;

using BetterDeaths.Capture.FullPull;
using BetterDeaths.Domain;

public sealed class DalamudLiveEventNormalizerTests
{
    private static readonly DateTimeOffset PullStart = new(2026, 8, 18, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ActionFactsProduceDeterministicTypedEventsAndStableActorIds()
    {
        var recorder = BeginRecorder();
        var normalizer = new DalamudLiveEventNormalizer(recorder, PullStart, "live-test");
        var player = Actor("party:content:1", "Player", ActorKind.Player, 38, "DNC");
        var boss = Actor("object:boss:1", "Boss", ActorKind.Enemy);
        recorder.MarkCombatObserved();

        normalizer.Append(new LiveActionEffectFact
        {
            ObservedAt = PullStart.AddSeconds(1),
            Kind = LiveActionEffectKind.Damage,
            Source = boss,
            Target = player,
            ActionId = 100,
            Amount = 12345,
            IsCritical = true,
            Fidelity = CaptureFidelity.Exact,
        });
        normalizer.Append(new LiveActionEffectFact
        {
            ObservedAt = PullStart.AddSeconds(1),
            Kind = LiveActionEffectKind.Heal,
            Source = player,
            Target = player,
            ActionId = 200,
            Amount = 5000,
            Fidelity = CaptureFidelity.Derived,
            Confidence = 0.9f,
        });
        normalizer.Append(new LiveActionEffectFact
        {
            ObservedAt = PullStart.AddSeconds(1),
            Kind = LiveActionEffectKind.ActionUse,
            Source = player,
            Target = boss,
            ActionId = 300,
        });

        Assert.True(recorder.TryFinalize(new PullEndContext(TimeSpan.FromSeconds(5)), out var pull));
        Assert.NotNull(pull);
        Assert.Equal(3, pull.Events.Count);
        Assert.Equal(new long[] { 1, 2, 3 }, pull.Events.Select(evt => evt.Sequence));
        Assert.Equal(new[] { new EventId(1), new EventId(2), new EventId(3) }, pull.Events.Select(evt => evt.Id));
        Assert.All(pull.Events, evt => Assert.Equal(TimeSpan.FromSeconds(1), evt.PullTime));

        var damage = Assert.IsType<DamageEvent>(pull.Events[0]);
        var heal = Assert.IsType<HealEvent>(pull.Events[1]);
        var action = Assert.IsType<ActionUseEvent>(pull.Events[2]);
        Assert.Equal(12345, damage.Amount);
        Assert.True(damage.IsCritical);
        Assert.Equal(damage.TargetActorId, heal.SourceActorId);
        Assert.Equal(heal.SourceActorId, heal.TargetActorId);
        Assert.Equal(heal.SourceActorId, action.SourceActorId);
        Assert.Equal(damage.SourceActorId, action.TargetActorId);
        Assert.Equal(PullDataSourceKind.DalamudLive, damage.Provenance.SourceKind);
        Assert.Equal("live-test", damage.Provenance.SourceReference);
        Assert.Equal(CaptureFidelity.Derived, heal.Provenance.Fidelity);
        Assert.Equal(0.9f, heal.Provenance.Confidence);
        Assert.Equal(2, pull.Actors.Count);
    }

    [Fact]
    public void PetOwnerRelationshipIsResolvedBeforePetAndSurvivesFinalization()
    {
        var recorder = BeginRecorder();
        var normalizer = new DalamudLiveEventNormalizer(recorder, PullStart);
        var owner = Actor("party:content:owner", "Owner", ActorKind.Player, 28, "SCH");
        var pet = Actor("pet:instance:1", "Seraph", ActorKind.Pet) with { Owner = owner };
        var boss = Actor("object:boss:1", "Boss", ActorKind.Enemy);
        recorder.MarkCombatObserved();

        normalizer.Append(new LiveActionEffectFact
        {
            ObservedAt = PullStart.AddSeconds(2),
            Kind = LiveActionEffectKind.Damage,
            Source = pet,
            Target = boss,
            ActionId = 400,
            Amount = 1000,
        });

        Assert.True(recorder.TryFinalize(new PullEndContext(TimeSpan.FromSeconds(5)), out var pull));
        Assert.NotNull(pull);
        var ownerRecord = Assert.Single(pull.Actors, actor => actor.Name == "Owner");
        var petRecord = Assert.Single(pull.Actors, actor => actor.Name == "Seraph");
        Assert.Equal(ownerRecord.Id, petRecord.OwnerActorId);
        Assert.Equal(3, pull.Actors.Count);
    }

    [Fact]
    public void SourceKeyReuseWithConflictingMetadataIsRejected()
    {
        var recorder = BeginRecorder();
        var normalizer = new DalamudLiveEventNormalizer(recorder, PullStart);
        var first = Actor("object:instance:7", "First Boss", ActorKind.Enemy);
        var conflicting = Actor("object:instance:7", "Second Boss", ActorKind.Enemy);
        var player = Actor("party:1", "Player", ActorKind.Player);

        normalizer.Append(new LiveActionEffectFact
        {
            ObservedAt = PullStart.AddSeconds(1),
            Kind = LiveActionEffectKind.Damage,
            Source = first,
            Target = player,
            ActionId = 1,
            Amount = 1,
        });

        var error = Assert.Throws<InvalidOperationException>(() => normalizer.Append(new LiveActionEffectFact
        {
            ObservedAt = PullStart.AddSeconds(2),
            Kind = LiveActionEffectKind.Damage,
            Source = conflicting,
            Target = player,
            ActionId = 2,
            Amount = 1,
        }));

        Assert.Contains("distinct stable key", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RepresentativeLiveFactsTranslateAcrossEventsAndSpatialSamplesWithOneSequenceAxis()
    {
        var recorder = BeginRecorder();
        var normalizer = new DalamudLiveEventNormalizer(recorder, PullStart, "dalamud:fixture");
        var player = Actor("party:1", "Player", ActorKind.Player);
        var boss = Actor("enemy:1", "Boss", ActorKind.Enemy);
        recorder.MarkCombatObserved();

        normalizer.Append(new LiveStatusFact
        {
            ObservedAt = PullStart.AddSeconds(1),
            Source = boss,
            Target = player,
            StatusId = 10,
            Applied = true,
            Stacks = 2,
            Duration = TimeSpan.FromSeconds(15),
        });
        normalizer.Append(new LiveStatusFact
        {
            ObservedAt = PullStart.AddSeconds(2),
            Source = boss,
            Target = player,
            StatusId = 10,
            Applied = false,
        });
        normalizer.Append(new LiveTargetabilityFact
        {
            ObservedAt = PullStart.AddSeconds(3),
            Actor = boss,
            IsTargetable = false,
        });
        normalizer.Append(new LiveGaugeFact
        {
            ObservedAt = PullStart.AddSeconds(4),
            Actor = player,
            GaugeKey = "esprit",
            Value = 80,
            Fidelity = CaptureFidelity.Derived,
        });
        normalizer.Append(new LiveTetherFact
        {
            ObservedAt = PullStart.AddSeconds(5),
            Source = boss,
            Target = player,
            TetherId = 84,
        });
        normalizer.Append(new LiveMarkerFact
        {
            ObservedAt = PullStart.AddSeconds(6),
            Target = player,
            MarkerId = 17,
        });
        normalizer.Append(new LiveMechanicSignalFact
        {
            ObservedAt = PullStart.AddSeconds(7),
            Source = boss,
            SignalKey = "map-state",
            SignalId = 500,
            State = 2,
            Fidelity = CaptureFidelity.Inferred,
            Confidence = 1.5f,
        });
        normalizer.Append(new LiveDeathFact
        {
            ObservedAt = PullStart.AddSeconds(8),
            Source = boss,
            Target = player,
        });
        normalizer.Append(new LiveRaiseFact
        {
            ObservedAt = PullStart.AddSeconds(9),
            Source = player,
            Target = player,
            ActionId = 125,
        });
        normalizer.Append(new LivePositionFact
        {
            ObservedAt = PullStart.AddSeconds(10),
            Actor = player,
            X = 101,
            Y = 0,
            Z = 99,
            Rotation = 1.2f,
            Fidelity = CaptureFidelity.Sampled,
        });
        normalizer.Append(new LiveWorldMarkerFact
        {
            ObservedAt = PullStart.AddSeconds(11),
            MarkerIndex = 2,
            Label = "C",
            Active = true,
            X = 95,
            Y = 0,
            Z = 100,
            Fidelity = CaptureFidelity.Sampled,
        });

        Assert.True(recorder.TryFinalize(new PullEndContext(TimeSpan.FromSeconds(12)), out var pull));
        Assert.NotNull(pull);
        Assert.Collection(
            pull.Events,
            evt => Assert.IsType<StatusApplyEvent>(evt),
            evt => Assert.IsType<StatusRemoveEvent>(evt),
            evt => Assert.IsType<TargetabilityEvent>(evt),
            evt => Assert.IsType<GaugeEvent>(evt),
            evt => Assert.IsType<TetherEvent>(evt),
            evt => Assert.IsType<MarkerEvent>(evt),
            evt => Assert.IsType<MechanicSignalEvent>(evt),
            evt => Assert.IsType<DeathEvent>(evt),
            evt => Assert.IsType<RaiseEvent>(evt));
        Assert.Equal(Enumerable.Range(1, 9).Select(value => (long)value), pull.Events.Select(evt => evt.Sequence));

        var mechanic = Assert.IsType<MechanicSignalEvent>(pull.Events[6]);
        Assert.Equal(1.0f, mechanic.Provenance.Confidence);
        Assert.Equal(CaptureFidelity.Inferred, mechanic.Provenance.Fidelity);

        var position = Assert.Single(pull.Positions);
        Assert.Equal(10, position.Sequence);
        Assert.Equal(TimeSpan.FromSeconds(10), position.PullTime);
        Assert.Equal(CaptureFidelity.Sampled, position.Provenance.Fidelity);

        var marker = Assert.Single(pull.WorldMarkers);
        Assert.Equal(11, marker.Sequence);
        Assert.Equal("C", marker.Label);
    }

    [Fact]
    public void PullRelativeTimeClampsPreStartWallClockButSequenceRemainsAppendOrdered()
    {
        var recorder = BeginRecorder();
        var normalizer = new DalamudLiveEventNormalizer(recorder, PullStart);
        var player = Actor("party:1", "Player", ActorKind.Player);
        var boss = Actor("enemy:1", "Boss", ActorKind.Enemy);
        recorder.MarkCombatObserved();

        normalizer.Append(new LiveActionEffectFact
        {
            ObservedAt = PullStart.AddMilliseconds(-50),
            Kind = LiveActionEffectKind.Damage,
            Source = boss,
            Target = player,
            ActionId = 1,
            Amount = 1,
        });
        normalizer.Append(new LiveActionEffectFact
        {
            ObservedAt = PullStart,
            Kind = LiveActionEffectKind.Damage,
            Source = boss,
            Target = player,
            ActionId = 2,
            Amount = 1,
        });

        Assert.True(recorder.TryFinalize(new PullEndContext(TimeSpan.FromSeconds(2)), out var pull));
        Assert.NotNull(pull);
        Assert.Equal(new long[] { 1, 2 }, pull.Events.Select(evt => evt.Sequence));
        Assert.All(pull.Events, evt => Assert.Equal(TimeSpan.Zero, evt.PullTime));
    }

    private static FullPullRecorder BeginRecorder()
    {
        var recorder = new FullPullRecorder();
        recorder.Begin(new PullStartContext
        {
            PullId = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            Metadata = new PullMetadata
            {
                TerritoryId = 123,
                TerritoryName = "Test Duty",
                StartedAt = PullStart,
            },
            SchemaVersion = new PullSchemaVersion(1),
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = "live-test",
                Fidelity = CaptureFidelity.Exact,
            },
            DutyActive = true,
        });
        return recorder;
    }

    private static LiveActorReference Actor(
        string stableKey,
        string name,
        ActorKind kind,
        uint? classJobId = null,
        string? jobAbbreviation = null)
    {
        return new LiveActorReference
        {
            StableKey = stableKey,
            Name = name,
            Kind = kind,
            ClassJobId = classJobId,
            JobAbbreviation = jobAbbreviation,
        };
    }
}
