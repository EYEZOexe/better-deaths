namespace BetterDeaths;

using BetterDeaths.Capture.FullPull;
using BetterDeaths.Domain;

public sealed class LiveSnapshotDeltaTrackerTests
{
    [Fact]
    public void TargetabilityEmitsInitialObservationAndOnlyRealTransitions()
    {
        var tracker = new LiveSnapshotDeltaTracker();
        var actor = Actor("boss:1", "Boss", ActorKind.Enemy);
        var start = DateTimeOffset.Parse("2026-08-18T18:00:00Z");

        var initial = tracker.ObserveTargetability(start, actor, true);
        var duplicate = tracker.ObserveTargetability(start.AddSeconds(1), actor, true);
        var transition = tracker.ObserveTargetability(start.AddSeconds(2), actor, false);

        Assert.NotNull(initial);
        Assert.True(initial.IsTargetable);
        Assert.Equal(CaptureFidelity.Sampled, initial.Fidelity);
        Assert.Equal(0.9f, initial.Confidence);
        Assert.Null(duplicate);
        Assert.NotNull(transition);
        Assert.False(transition.IsTargetable);
        Assert.Equal(start.AddSeconds(2), transition.ObservedAt);
    }

    [Fact]
    public void StatusSnapshotsEmitApplyAndRemovalDeltasWithoutRepeatingStableState()
    {
        var tracker = new LiveSnapshotDeltaTracker();
        var target = Actor("party:1", "Player", ActorKind.Player);
        var source = Actor("party:2", "Healer", ActorKind.Player);
        var start = DateTimeOffset.Parse("2026-08-18T18:00:00Z");
        var status = ObservedStatus(100, source, stacks: 1, remainingSeconds: 20);

        var initial = tracker.ObserveStatuses(start, target, [status]);
        var unchanged = tracker.ObserveStatuses(
            start.AddSeconds(1),
            target,
            [ObservedStatus(100, source, stacks: 1, remainingSeconds: 19)]);
        var removed = tracker.ObserveStatuses(start.AddSeconds(2), target, []);

        var apply = Assert.Single(initial);
        Assert.True(apply.Applied);
        Assert.Equal((uint)100, apply.StatusId);
        Assert.Equal(TimeSpan.FromSeconds(20), apply.Duration);
        Assert.Equal(source.StableKey, apply.Source?.StableKey);
        Assert.Equal(CaptureFidelity.Sampled, apply.Fidelity);
        Assert.Equal(0.85f, apply.Confidence);
        Assert.Empty(unchanged);

        var removal = Assert.Single(removed);
        Assert.False(removal.Applied);
        Assert.Equal((uint)100, removal.StatusId);
        Assert.Null(removal.Duration);
    }

    [Fact]
    public void StackChangeEmitsSampledRefreshWithoutSyntheticRemoval()
    {
        var tracker = new LiveSnapshotDeltaTracker();
        var target = Actor("party:1", "Player", ActorKind.Player);
        var start = DateTimeOffset.Parse("2026-08-18T18:00:00Z");

        _ = tracker.ObserveStatuses(start, target, [ObservedStatus(200, null, stacks: 1, remainingSeconds: 15)]);
        var changed = tracker.ObserveStatuses(
            start.AddSeconds(1),
            target,
            [ObservedStatus(200, null, stacks: 2, remainingSeconds: 14)]);

        var refresh = Assert.Single(changed);
        Assert.True(refresh.Applied);
        Assert.Equal((ushort)2, refresh.Stacks);
        Assert.Equal(TimeSpan.FromSeconds(14), refresh.Duration);
    }

    [Fact]
    public void StatusSourcesRemainDistinctWhenSameStatusIdIsObserved()
    {
        var tracker = new LiveSnapshotDeltaTracker();
        var target = Actor("party:1", "Player", ActorKind.Player);
        var sourceA = Actor("party:2", "Healer A", ActorKind.Player);
        var sourceB = Actor("party:3", "Healer B", ActorKind.Player);
        var start = DateTimeOffset.Parse("2026-08-18T18:00:00Z");

        var initial = tracker.ObserveStatuses(
            start,
            target,
            [
                ObservedStatus(300, sourceA, 1, 10),
                ObservedStatus(300, sourceB, 1, 10),
            ]);
        var next = tracker.ObserveStatuses(
            start.AddSeconds(1),
            target,
            [ObservedStatus(300, sourceB, 1, 9)]);

        Assert.Equal(2, initial.Count);
        var removed = Assert.Single(next);
        Assert.False(removed.Applied);
        Assert.Equal(sourceA.StableKey, removed.Source?.StableKey);
    }

    [Fact]
    public void ResetPreventsCrossPullDeltaLeakage()
    {
        var tracker = new LiveSnapshotDeltaTracker();
        var actor = Actor("boss:1", "Boss", ActorKind.Enemy);
        var target = Actor("party:1", "Player", ActorKind.Player);
        var start = DateTimeOffset.Parse("2026-08-18T18:00:00Z");

        _ = tracker.ObserveTargetability(start, actor, false);
        _ = tracker.ObserveStatuses(start, target, [ObservedStatus(400, actor, 1, 20)]);
        tracker.Reset();

        var targetability = tracker.ObserveTargetability(start.AddMinutes(1), actor, false);
        var status = tracker.ObserveStatuses(
            start.AddMinutes(1),
            target,
            [ObservedStatus(400, actor, 1, 20)]);

        Assert.NotNull(targetability);
        Assert.Single(status);
        Assert.True(status[0].Applied);
    }

    private static LiveActorReference Actor(string stableKey, string name, ActorKind kind)
    {
        return new LiveActorReference
        {
            StableKey = stableKey,
            Name = name,
            Kind = kind,
        };
    }

    private static LiveObservedStatus ObservedStatus(
        uint statusId,
        LiveActorReference? source,
        ushort stacks,
        double remainingSeconds)
    {
        return new LiveObservedStatus
        {
            Source = source,
            StatusId = statusId,
            Stacks = stacks,
            RemainingDuration = TimeSpan.FromSeconds(remainingSeconds),
        };
    }
}
