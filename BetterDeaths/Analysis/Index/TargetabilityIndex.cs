namespace BetterDeaths.Analysis.Index;

using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

internal sealed record TargetabilityInterval
{
    public required ActorId ActorId { get; init; }

    public required TimeSpan Start { get; init; }

    public required TimeSpan End { get; init; }

    public required bool IsTargetable { get; init; }

    public required IReadOnlyList<EventId> EvidenceEventIds { get; init; }

    public TimeSpan Duration => End > Start ? End - Start : TimeSpan.Zero;
}

internal readonly record struct TargetabilityCoverage(
    TimeSpan TargetableDuration,
    TimeSpan UntargetableDuration,
    TimeSpan UnknownDuration,
    IReadOnlyList<EventId> EvidenceEventIds)
{
    public TimeSpan KnownDuration => TargetableDuration + UntargetableDuration;

    public TimeSpan TotalDuration => KnownDuration + UnknownDuration;

    public double KnownFraction => TotalDuration <= TimeSpan.Zero
        ? 1.0
        : KnownDuration.TotalSeconds / TotalDuration.TotalSeconds;
}

internal sealed class TargetabilityIndex
{
    private static readonly IReadOnlyList<TargetabilityInterval> EmptyIntervals = Array.Empty<TargetabilityInterval>();

    private readonly TimeSpan pullDuration;
    private readonly IReadOnlyDictionary<ActorId, IReadOnlyList<TargetabilityInterval>> intervalsByActor;

    public TargetabilityIndex(EventIndex events, TimeSpan pullDuration)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (pullDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pullDuration));
        }

        this.pullDuration = pullDuration;
        var eventsByActor = new Dictionary<ActorId, List<TargetabilityEvent>>();
        foreach (var evt in events.OfType<TargetabilityEvent>())
        {
            ValidateEventTime(evt, pullDuration);
            var actorId = evt.TargetActorId ?? evt.SourceActorId
                ?? throw new InvalidOperationException(
                    $"Canonical targetability event {evt.Id.Value} does not reference an actor.");
            if (!eventsByActor.TryGetValue(actorId, out var actorEvents))
            {
                actorEvents = [];
                eventsByActor.Add(actorId, actorEvents);
            }

            actorEvents.Add(evt);
        }

        intervalsByActor = eventsByActor.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<TargetabilityInterval>)BuildIntervals(pair.Key, pair.Value, pullDuration));
    }

    public IReadOnlyList<TargetabilityInterval> ForActor(ActorId actorId)
    {
        return intervalsByActor.TryGetValue(actorId, out var intervals)
            ? intervals
            : EmptyIntervals;
    }

    public TargetabilityCoverage GetCoverage(ActorId actorId)
    {
        return GetCoverage(actorId, new TimeRange(TimeSpan.Zero, pullDuration));
    }

    public TargetabilityCoverage GetCoverage(ActorId actorId, TimeRange window)
    {
        var clipped = ClipWindow(window, pullDuration);
        if (clipped.End <= clipped.Start)
        {
            return new TargetabilityCoverage(
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                Array.Empty<EventId>());
        }

        var targetable = TimeSpan.Zero;
        var untargetable = TimeSpan.Zero;
        var evidence = new List<EventId>();
        foreach (var interval in ForActor(actorId))
        {
            var duration = IntersectionDuration(interval.Start, interval.End, clipped.Start, clipped.End);
            if (duration <= TimeSpan.Zero)
            {
                continue;
            }

            if (interval.IsTargetable)
            {
                targetable += duration;
            }
            else
            {
                untargetable += duration;
            }

            evidence.AddRange(interval.EvidenceEventIds);
        }

        var total = clipped.End - clipped.Start;
        var known = targetable + untargetable;
        var unknown = total > known ? total - known : TimeSpan.Zero;
        return new TargetabilityCoverage(
            targetable,
            untargetable,
            unknown,
            evidence.Distinct().ToArray());
    }

    private static TargetabilityInterval[] BuildIntervals(
        ActorId actorId,
        IReadOnlyList<TargetabilityEvent> events,
        TimeSpan pullDuration)
    {
        if (events.Count == 0)
        {
            return [];
        }

        var intervals = new List<TargetabilityInterval>();
        var current = events[0];
        var currentStart = current.PullTime;
        var lastEventTime = current.PullTime;
        var currentEvidence = new List<EventId> { current.Id };

        for (var index = 1; index < events.Count; index++)
        {
            var next = events[index];
            if (next.PullTime < lastEventTime)
            {
                throw new InvalidOperationException(
                    $"Targetability event time moved backwards for actor {actorId.Value}: " +
                    $"{lastEventTime} -> {next.PullTime}.");
            }

            lastEventTime = next.PullTime;
            if (next.IsTargetable == current.IsTargetable)
            {
                currentEvidence.Add(next.Id);
                continue;
            }

            intervals.Add(new TargetabilityInterval
            {
                ActorId = actorId,
                Start = currentStart,
                End = next.PullTime,
                IsTargetable = current.IsTargetable,
                EvidenceEventIds = currentEvidence.ToArray(),
            });
            current = next;
            currentStart = next.PullTime;
            currentEvidence = [next.Id];
        }

        intervals.Add(new TargetabilityInterval
        {
            ActorId = actorId,
            Start = currentStart,
            End = pullDuration,
            IsTargetable = current.IsTargetable,
            EvidenceEventIds = currentEvidence.ToArray(),
        });
        return intervals.ToArray();
    }

    private static void ValidateEventTime(TargetabilityEvent evt, TimeSpan pullDuration)
    {
        if (evt.PullTime < TimeSpan.Zero || evt.PullTime > pullDuration)
        {
            throw new InvalidOperationException(
                $"Canonical targetability event {evt.Id.Value} lies outside pull bounds: {evt.PullTime} / {pullDuration}.");
        }
    }

    private static TimeRange ClipWindow(TimeRange window, TimeSpan pullDuration)
    {
        if (window.End < window.Start)
        {
            throw new ArgumentException("Analysis time range end cannot precede start.", nameof(window));
        }

        var start = window.Start < TimeSpan.Zero ? TimeSpan.Zero : window.Start;
        var end = window.End > pullDuration ? pullDuration : window.End;
        if (start > pullDuration)
        {
            start = pullDuration;
        }

        if (end < TimeSpan.Zero)
        {
            end = TimeSpan.Zero;
        }

        return new TimeRange(start, end);
    }

    private static TimeSpan IntersectionDuration(
        TimeSpan firstStart,
        TimeSpan firstEnd,
        TimeSpan secondStart,
        TimeSpan secondEnd)
    {
        var start = firstStart > secondStart ? firstStart : secondStart;
        var end = firstEnd < secondEnd ? firstEnd : secondEnd;
        return end > start ? end - start : TimeSpan.Zero;
    }
}
