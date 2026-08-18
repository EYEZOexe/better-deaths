namespace BetterDeaths.Analysis.Index;

using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

internal readonly record struct StatusIntervalKey(
    ActorId ActorId,
    uint StatusId,
    ActorId? SourceActorId);

internal enum StatusIntervalEndReason
{
    Removed,
    DurationExpired,
    Reapplied,
    PullEndedBeforeKnownExpiry,
    PullEndedWithUnknownStatusEnd,
}

internal sealed record StatusInterval
{
    public required StatusIntervalKey Key { get; init; }

    public required TimeSpan Start { get; init; }

    public required TimeSpan End { get; init; }

    public required ushort Stacks { get; init; }

    public required StatusIntervalEndReason EndReason { get; init; }

    public required bool CoverageKnownThroughEnd { get; init; }

    public required IReadOnlyList<EventId> EvidenceEventIds { get; init; }

    public TimeSpan Duration => End > Start ? End - Start : TimeSpan.Zero;
}

internal sealed class StatusIntervalIndex
{
    private static readonly IReadOnlyList<StatusInterval> EmptyIntervals = Array.Empty<StatusInterval>();

    private readonly TimeSpan pullDuration;
    private readonly IReadOnlyDictionary<StatusIntervalKey, IReadOnlyList<StatusInterval>> intervalsByKey;

    public StatusIntervalIndex(EventIndex events, TimeSpan pullDuration)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (pullDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pullDuration));
        }

        this.pullDuration = pullDuration;
        var timelineEvents = events.All
            .Where(evt => evt is StatusApplyEvent or StatusRemoveEvent)
            .ToArray();
        var builders = new Dictionary<StatusIntervalKey, StatusTimelineBuilder>();
        var unmatchedRemovals = new List<EventId>();

        foreach (var evt in timelineEvents)
        {
            ValidateEventTime(evt, pullDuration);
            var targetActorId = evt.TargetActorId
                ?? throw new InvalidOperationException(
                    $"Canonical status event {evt.Id.Value} does not reference a target actor.");
            var statusId = evt switch
            {
                StatusApplyEvent apply => apply.StatusId,
                StatusRemoveEvent remove => remove.StatusId,
                _ => throw new InvalidOperationException("Unexpected status event type."),
            };
            var key = new StatusIntervalKey(targetActorId, statusId, evt.SourceActorId);
            if (!builders.TryGetValue(key, out var builder))
            {
                builder = new StatusTimelineBuilder(key, pullDuration);
                builders.Add(key, builder);
            }

            if (evt is StatusApplyEvent statusApply)
            {
                builder.Apply(statusApply);
            }
            else if (!builder.Remove((StatusRemoveEvent)evt))
            {
                unmatchedRemovals.Add(evt.Id);
            }
        }

        foreach (var builder in builders.Values)
        {
            builder.Complete();
        }

        intervalsByKey = builders.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<StatusInterval>)pair.Value.Intervals.ToArray());
        UnmatchedRemovalEventIds = unmatchedRemovals.ToArray();
    }

    public IReadOnlyList<EventId> UnmatchedRemovalEventIds { get; }

    public IReadOnlyList<StatusInterval> ForKey(StatusIntervalKey key)
    {
        return intervalsByKey.TryGetValue(key, out var intervals)
            ? intervals
            : EmptyIntervals;
    }

    public IReadOnlyList<StatusInterval> ForActorStatus(ActorId actorId, uint statusId)
    {
        return intervalsByKey
            .Where(pair => pair.Key.ActorId == actorId && pair.Key.StatusId == statusId)
            .SelectMany(pair => pair.Value)
            .OrderBy(interval => interval.Start)
            .ThenBy(interval => interval.Key.SourceActorId?.Value ?? int.MinValue)
            .ToArray();
    }

    public TimeSpan GetKnownActiveDuration(StatusIntervalKey key)
    {
        return GetKnownActiveDuration(key, new TimeRange(TimeSpan.Zero, pullDuration));
    }

    public TimeSpan GetKnownActiveDuration(StatusIntervalKey key, TimeRange window)
    {
        var clipped = ClipWindow(window, pullDuration);
        var duration = TimeSpan.Zero;
        foreach (var interval in ForKey(key))
        {
            if (!interval.CoverageKnownThroughEnd)
            {
                continue;
            }

            duration += IntersectionDuration(interval.Start, interval.End, clipped.Start, clipped.End);
        }

        return duration;
    }

    private static void ValidateEventTime(NormalizedEvent evt, TimeSpan pullDuration)
    {
        if (evt.PullTime < TimeSpan.Zero || evt.PullTime > pullDuration)
        {
            throw new InvalidOperationException(
                $"Canonical status event {evt.Id.Value} lies outside pull bounds: {evt.PullTime} / {pullDuration}.");
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

    private sealed class StatusTimelineBuilder
    {
        private readonly StatusIntervalKey key;
        private readonly TimeSpan pullDuration;
        private ActiveStatus? active;
        private TimeSpan lastEventTime = TimeSpan.Zero;

        public StatusTimelineBuilder(StatusIntervalKey key, TimeSpan pullDuration)
        {
            this.key = key;
            this.pullDuration = pullDuration;
        }

        public List<StatusInterval> Intervals { get; } = [];

        public void Apply(StatusApplyEvent evt)
        {
            ValidateOrderedTime(evt.PullTime);
            ExpireBefore(evt.PullTime);
            if (active is not null)
            {
                CloseActive(
                    evt.PullTime,
                    StatusIntervalEndReason.Reapplied,
                    coverageKnownThroughEnd: true,
                    closingEvidence: evt.Id);
            }

            if (evt.Duration is { } duration && duration < TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    $"Canonical status apply event {evt.Id.Value} has a negative duration {duration}.");
            }

            active = new ActiveStatus(
                evt.PullTime,
                evt.Stacks,
                evt.Duration is { } knownDuration ? evt.PullTime + knownDuration : null,
                evt.Id);
        }

        public bool Remove(StatusRemoveEvent evt)
        {
            ValidateOrderedTime(evt.PullTime);
            ExpireBefore(evt.PullTime);
            if (active is null)
            {
                return false;
            }

            CloseActive(
                evt.PullTime,
                StatusIntervalEndReason.Removed,
                coverageKnownThroughEnd: true,
                closingEvidence: evt.Id);
            return true;
        }

        public void Complete()
        {
            if (active is null)
            {
                return;
            }

            if (active.ExpectedEnd is { } expectedEnd && expectedEnd <= pullDuration)
            {
                CloseActive(
                    expectedEnd,
                    StatusIntervalEndReason.DurationExpired,
                    coverageKnownThroughEnd: true,
                    closingEvidence: null);
                return;
            }

            if (active.ExpectedEnd is not null)
            {
                CloseActive(
                    pullDuration,
                    StatusIntervalEndReason.PullEndedBeforeKnownExpiry,
                    coverageKnownThroughEnd: true,
                    closingEvidence: null);
                return;
            }

            CloseActive(
                pullDuration,
                StatusIntervalEndReason.PullEndedWithUnknownStatusEnd,
                coverageKnownThroughEnd: false,
                closingEvidence: null);
        }

        private void ExpireBefore(TimeSpan eventTime)
        {
            if (active?.ExpectedEnd is not { } expectedEnd || expectedEnd >= eventTime)
            {
                return;
            }

            CloseActive(
                expectedEnd,
                StatusIntervalEndReason.DurationExpired,
                coverageKnownThroughEnd: true,
                closingEvidence: null);
        }

        private void CloseActive(
            TimeSpan end,
            StatusIntervalEndReason reason,
            bool coverageKnownThroughEnd,
            EventId? closingEvidence)
        {
            var current = active ?? throw new InvalidOperationException("No active status interval exists.");
            var evidence = closingEvidence is { } eventId
                ? new[] { current.OpeningEventId, eventId }
                : new[] { current.OpeningEventId };
            Intervals.Add(new StatusInterval
            {
                Key = key,
                Start = current.Start,
                End = end,
                Stacks = current.Stacks,
                EndReason = reason,
                CoverageKnownThroughEnd = coverageKnownThroughEnd,
                EvidenceEventIds = evidence,
            });
            active = null;
        }

        private void ValidateOrderedTime(TimeSpan eventTime)
        {
            if (eventTime < lastEventTime)
            {
                throw new InvalidOperationException(
                    $"Status event time moved backwards for actor {key.ActorId.Value}, status {key.StatusId}: " +
                    $"{lastEventTime} -> {eventTime}.");
            }

            lastEventTime = eventTime;
        }

        private sealed record ActiveStatus(
            TimeSpan Start,
            ushort Stacks,
            TimeSpan? ExpectedEnd,
            EventId OpeningEventId);
    }
}
