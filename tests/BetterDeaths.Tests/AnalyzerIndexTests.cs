namespace BetterDeaths;

using BetterDeaths.Analysis.Index;
using BetterDeaths.Domain;

public sealed class AnalyzerIndexTests
{
    [Fact]
    public void ActorIndexProvidesStableLookupAndRejectsDuplicateIds()
    {
        var player = Actor(1, "Player");
        var boss = Actor(2, "Boss", ActorKind.Enemy);
        var index = new ActorIndex([player, boss]);

        Assert.Equal(2, index.Count);
        Assert.True(index.TryGet(player.Id, out var found));
        Assert.Equal(player, found);
        Assert.Equal(boss, index.GetRequired(boss.Id));
        Assert.Throws<KeyNotFoundException>(() => index.GetRequired(new ActorId(999)));

        var duplicate = boss with { Id = player.Id };
        var error = Assert.Throws<InvalidOperationException>(() => new ActorIndex([player, duplicate]));
        Assert.Contains("Duplicate canonical actor ID", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EventIndexPreservesCanonicalOrderAndIndexesTypesActorsActionsAndStatuses()
    {
        var player = new ActorId(1);
        var boss = new ActorId(2);
        NormalizedEvent[] events =
        [
            Damage(1, boss, player, actionId: 100),
            new StatusApplyEvent
            {
                Id = new EventId(2),
                Sequence = 2,
                PullTime = TimeSpan.FromSeconds(2),
                SourceActorId = boss,
                TargetActorId = player,
                Provenance = Provenance(),
                StatusId = 500,
                Duration = TimeSpan.FromSeconds(10),
            },
            new ActionUseEvent
            {
                Id = new EventId(3),
                Sequence = 3,
                PullTime = TimeSpan.FromSeconds(3),
                SourceActorId = player,
                TargetActorId = boss,
                Provenance = Provenance(),
                ActionId = 100,
            },
            new StatusRemoveEvent
            {
                Id = new EventId(4),
                Sequence = 4,
                PullTime = TimeSpan.FromSeconds(4),
                SourceActorId = boss,
                TargetActorId = player,
                Provenance = Provenance(),
                StatusId = 500,
            },
            new DeathEvent
            {
                Id = new EventId(5),
                Sequence = 5,
                PullTime = TimeSpan.FromSeconds(5),
                SourceActorId = boss,
                TargetActorId = player,
                Provenance = Provenance(),
            },
        ];

        var index = new EventIndex(events);

        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, index.All.Select(evt => evt.Sequence));
        Assert.Single(index.OfType<DamageEvent>());
        Assert.Single(index.OfType<ActionUseEvent>());
        Assert.Single(index.OfType<DeathEvent>());
        Assert.Empty(index.OfType<HealEvent>());

        Assert.Equal(new long[] { 1, 2, 4, 5 }, index.FromActor(boss).Select(evt => evt.Sequence));
        Assert.Equal(new long[] { 1, 2, 4, 5 }, index.ToActor(player).Select(evt => evt.Sequence));
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, index.InvolvingActor(player).Select(evt => evt.Sequence));
        Assert.Equal(new long[] { 1, 3 }, index.ByAction(100).Select(evt => evt.Sequence));
        Assert.Equal(new long[] { 2, 4 }, index.ByStatus(500).Select(evt => evt.Sequence));
        Assert.Empty(index.ByAction(999));
        Assert.Empty(index.ByStatus(999));
    }

    [Fact]
    public void InvolvingActorDoesNotDuplicateSelfTargetedEvents()
    {
        var player = new ActorId(1);
        var evt = new HealEvent
        {
            Id = new EventId(1),
            Sequence = 1,
            PullTime = TimeSpan.FromSeconds(1),
            SourceActorId = player,
            TargetActorId = player,
            Provenance = Provenance(),
            Amount = 1000,
            ActionId = 200,
        };

        var index = new EventIndex([evt]);

        Assert.Equal(evt, Assert.Single(index.InvolvingActor(player)));
    }

    [Fact]
    public void EventIndexRejectsNonIncreasingSequence()
    {
        var player = new ActorId(1);
        var boss = new ActorId(2);
        var events = new NormalizedEvent[]
        {
            Damage(2, boss, player, 100),
            Damage(1, boss, player, 101),
        };

        var error = Assert.Throws<InvalidOperationException>(() => new EventIndex(events));

        Assert.Contains("sequence must increase strictly", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EventIndexRejectsDuplicateEventIdsEvenWithIncreasingSequence()
    {
        var player = new ActorId(1);
        var boss = new ActorId(2);
        var first = Damage(1, boss, player, 100);
        var second = Damage(2, boss, player, 101) with { Id = first.Id };

        var error = Assert.Throws<InvalidOperationException>(() => new EventIndex([first, second]));

        Assert.Contains("Duplicate canonical event ID", error.Message, StringComparison.Ordinal);
    }

    private static ActorRecord Actor(int id, string name, ActorKind kind = ActorKind.Player)
    {
        return new ActorRecord
        {
            Id = new ActorId(id),
            Name = name,
            Kind = kind,
        };
    }

    private static DamageEvent Damage(long sequence, ActorId source, ActorId target, uint actionId)
    {
        return new DamageEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(sequence),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = Provenance(),
            Amount = 1000,
            ActionId = actionId,
        };
    }

    private static EventProvenance Provenance()
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            Fidelity = CaptureFidelity.Exact,
        };
    }
}
