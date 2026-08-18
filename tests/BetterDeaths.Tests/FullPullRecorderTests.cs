namespace BetterDeaths;

using BetterDeaths.Capture.FullPull;
using BetterDeaths.Domain;

public sealed class FullPullRecorderTests
{
    [Theory]
    [InlineData(true, true, 1, 1.0, true)]
    [InlineData(true, true, 4, 20.0, true)]
    [InlineData(false, true, 1, 10.0, false)]
    [InlineData(true, false, 1, 10.0, false)]
    [InlineData(true, true, 0, 10.0, false)]
    [InlineData(true, true, 1, 0.99, false)]
    public void FinalizationPolicyRequiresDutyCombatRelevantEventAndMinimumDuration(
        bool dutyActive,
        bool combatObserved,
        int relevantEventCount,
        double durationSeconds,
        bool expected)
    {
        var facts = new PullFinalizationFacts(
            dutyActive,
            combatObserved,
            relevantEventCount,
            TimeSpan.FromSeconds(durationSeconds));

        Assert.Equal(expected, PullFinalizationPolicy.IsMeaningful(facts));
    }

    [Fact]
    public void MeaningfulZeroDeathPullFinalizesIntoCanonicalRecordedPull()
    {
        var recorder = new FullPullRecorder();
        var player = CreateActor(1, "Player", ActorKind.Player);
        var enemy = CreateActor(2, "Boss", ActorKind.Enemy);

        recorder.Begin(CreateStartContext("11111111-1111-1111-1111-111111111111"));
        recorder.MarkCombatObserved();
        recorder.RegisterActor(player);
        recorder.RegisterActor(enemy);
        recorder.Append(CreateDamageEvent(1, enemy.Id, player.Id));
        recorder.Append(CreatePosition(2, player.Id));
        recorder.Append(CreateWorldMarker(3));

        var finalized = recorder.TryFinalize(
            new PullEndContext(TimeSpan.FromSeconds(12)),
            out var pull);

        Assert.True(finalized);
        Assert.NotNull(pull);
        Assert.Equal(TimeSpan.FromSeconds(12), pull.Metadata.Duration);
        Assert.Equal(new PullSchemaVersion(1), pull.SchemaVersion);
        Assert.Equal(2, pull.Actors.Count);
        Assert.Single(pull.Events);
        Assert.DoesNotContain(pull.Events, evt => evt is DeathEvent);
        Assert.Single(pull.Positions);
        Assert.Single(pull.WorldMarkers);
        Assert.False(recorder.IsActive);
        Assert.Equal(0, recorder.EventCount);
        Assert.Equal(0, recorder.PositionCount);
        Assert.Equal(0, recorder.WorldMarkerCount);
    }

    [Fact]
    public void TrivialPullIsDiscardedAndRecorderIsReset()
    {
        var recorder = new FullPullRecorder();

        recorder.Begin(CreateStartContext("22222222-2222-2222-2222-222222222222"));
        recorder.MarkCombatObserved();
        recorder.Append(CreatePosition(1, new ActorId(1)));

        var finalized = recorder.TryFinalize(
            new PullEndContext(TimeSpan.FromSeconds(30)),
            out var pull);

        Assert.False(finalized);
        Assert.Null(pull);
        Assert.False(recorder.IsActive);
        Assert.Equal(0, recorder.PositionCount);
    }

    [Fact]
    public void SequenceMustIncreaseAcrossEventsAndSpatialSamples()
    {
        var recorder = new FullPullRecorder();
        recorder.Begin(CreateStartContext("33333333-3333-3333-3333-333333333333"));
        recorder.Append(CreateDamageEvent(1, new ActorId(2), new ActorId(1)));
        recorder.Append(CreatePosition(2, new ActorId(1)));

        var duplicate = CreateWorldMarker(2);

        var error = Assert.Throws<InvalidOperationException>(() => recorder.Append(duplicate));
        Assert.Contains("must increase strictly", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetPreventsCrossPullContaminationAndRestartsSequenceBoundary()
    {
        var recorder = new FullPullRecorder();
        var firstPlayer = CreateActor(1, "First", ActorKind.Player);

        recorder.Begin(CreateStartContext("44444444-4444-4444-4444-444444444444"));
        recorder.MarkCombatObserved();
        recorder.RegisterActor(firstPlayer);
        recorder.Append(CreateDamageEvent(10, new ActorId(2), firstPlayer.Id));
        recorder.Reset();

        var secondPlayer = CreateActor(7, "Second", ActorKind.Player);
        recorder.Begin(CreateStartContext("55555555-5555-5555-5555-555555555555"));
        recorder.MarkCombatObserved();
        recorder.RegisterActor(secondPlayer);
        recorder.Append(CreateDamageEvent(1, new ActorId(8), secondPlayer.Id));

        Assert.True(recorder.TryFinalize(new PullEndContext(TimeSpan.FromSeconds(5)), out var pull));
        Assert.NotNull(pull);
        Assert.Equal(new PullId(Guid.Parse("55555555-5555-5555-5555-555555555555")), pull.Id);
        Assert.Equal(secondPlayer, Assert.Single(pull.Actors));
        Assert.Equal(new EventId(1), Assert.Single(pull.Events).Id);
    }

    [Fact]
    public void ReRegisteringSameActorIsIdempotentButConflictingIdentityFails()
    {
        var recorder = new FullPullRecorder();
        var actor = CreateActor(1, "Player", ActorKind.Player);

        recorder.Begin(CreateStartContext("66666666-6666-6666-6666-666666666666"));
        recorder.RegisterActor(actor);
        recorder.RegisterActor(actor);

        var conflict = actor with { Name = "Different Player" };
        var error = Assert.Throws<InvalidOperationException>(() => recorder.RegisterActor(conflict));

        Assert.Contains("conflicting canonical data", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendAndCombatOperationsRequireActivePull()
    {
        var recorder = new FullPullRecorder();

        Assert.Throws<InvalidOperationException>(() => recorder.MarkCombatObserved());
        Assert.Throws<InvalidOperationException>(() => recorder.Append(CreateDamageEvent(1, new ActorId(2), new ActorId(1))));
        Assert.Throws<InvalidOperationException>(() => recorder.TryFinalize(new PullEndContext(TimeSpan.FromSeconds(5)), out _));
    }

    [Fact]
    public void BeginRejectsOverlappingPulls()
    {
        var recorder = new FullPullRecorder();
        recorder.Begin(CreateStartContext("77777777-7777-7777-7777-777777777777"));

        Assert.Throws<InvalidOperationException>(
            () => recorder.Begin(CreateStartContext("88888888-8888-8888-8888-888888888888")));
    }

    private static PullStartContext CreateStartContext(string pullId)
    {
        return new PullStartContext
        {
            PullId = new PullId(Guid.Parse(pullId)),
            Metadata = new PullMetadata
            {
                TerritoryId = 123,
                TerritoryName = "Test Duty",
                StartedAt = new DateTimeOffset(2026, 8, 18, 18, 0, 0, TimeSpan.Zero),
                Duration = TimeSpan.Zero,
            },
            SchemaVersion = new PullSchemaVersion(1),
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = "test",
                Fidelity = CaptureFidelity.Exact,
            },
            DutyActive = true,
        };
    }

    private static ActorRecord CreateActor(int id, string name, ActorKind kind)
    {
        return new ActorRecord
        {
            Id = new ActorId(id),
            Name = name,
            Kind = kind,
        };
    }

    private static DamageEvent CreateDamageEvent(long sequence, ActorId source, ActorId target)
    {
        return new DamageEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(sequence),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = EventProvenance(),
            Amount = 1000,
            ActionId = 100,
        };
    }

    private static PositionSample CreatePosition(long sequence, ActorId actorId)
    {
        return new PositionSample
        {
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(sequence),
            ActorId = actorId,
            X = 100,
            Y = 0,
            Z = 100,
            Provenance = EventProvenance(),
        };
    }

    private static WorldMarkerSample CreateWorldMarker(long sequence)
    {
        return new WorldMarkerSample
        {
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(sequence),
            MarkerIndex = 0,
            Label = "A",
            Active = true,
            X = 100,
            Y = 0,
            Z = 90,
            Provenance = EventProvenance(),
        };
    }

    private static EventProvenance EventProvenance()
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = "test",
            Fidelity = CaptureFidelity.Exact,
        };
    }
}
