namespace BetterDeaths;

using BetterDeaths.Capture.FullPull;
using BetterDeaths.Domain;

public sealed class LiveSnapshotDeltaRefreshTests
{
    [Fact]
    public void SameStackDurationIncreaseEmitsRefreshButNormalCountdownDoesNot()
    {
        var tracker = new LiveSnapshotDeltaTracker();
        var target = Actor("party:1", "Player");
        var source = Actor("party:2", "Healer");
        var start = DateTimeOffset.Parse("2026-08-18T18:00:00Z");

        _ = tracker.ObserveStatuses(start, target, [Status(source, 100, 1, 20)]);
        var countdown = tracker.ObserveStatuses(start.AddSeconds(1), target, [Status(source, 100, 1, 19)]);
        var refreshed = tracker.ObserveStatuses(start.AddSeconds(2), target, [Status(source, 100, 1, 30)]);

        Assert.Empty(countdown);
        var fact = Assert.Single(refreshed);
        Assert.True(fact.Applied);
        Assert.Equal(TimeSpan.FromSeconds(30), fact.Duration);
    }

    private static LiveActorReference Actor(string key, string name)
    {
        return new LiveActorReference
        {
            StableKey = key,
            Name = name,
            Kind = ActorKind.Player,
        };
    }

    private static LiveObservedStatus Status(
        LiveActorReference source,
        uint statusId,
        ushort stacks,
        double seconds)
    {
        return new LiveObservedStatus
        {
            Source = source,
            StatusId = statusId,
            Stacks = stacks,
            RemainingDuration = TimeSpan.FromSeconds(seconds),
        };
    }
}
