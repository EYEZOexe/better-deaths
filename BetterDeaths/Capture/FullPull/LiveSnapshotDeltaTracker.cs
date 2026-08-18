namespace BetterDeaths.Capture.FullPull;

using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

internal sealed record LiveObservedStatus
{
    public LiveActorReference? Source { get; init; }

    public required uint StatusId { get; init; }

    public ushort Stacks { get; init; }

    public TimeSpan? RemainingDuration { get; init; }
}

internal sealed class LiveSnapshotDeltaTracker
{
    private readonly Dictionary<string, bool> targetabilityByActor = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<ObservedStatusKey, LiveObservedStatus>> statusesByActor = new(StringComparer.Ordinal);

    public LiveTargetabilityFact? ObserveTargetability(
        DateTimeOffset observedAt,
        LiveActorReference actor,
        bool isTargetable)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (targetabilityByActor.TryGetValue(actor.StableKey, out var previous) && previous == isTargetable)
        {
            return null;
        }

        targetabilityByActor[actor.StableKey] = isTargetable;
        return new LiveTargetabilityFact
        {
            ObservedAt = observedAt,
            Actor = actor,
            IsTargetable = isTargetable,
            Fidelity = CaptureFidelity.Sampled,
            Confidence = 0.9f,
        };
    }

    public IReadOnlyList<LiveStatusFact> ObserveStatuses(
        DateTimeOffset observedAt,
        LiveActorReference target,
        IReadOnlyList<LiveObservedStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(statuses);

        var current = new Dictionary<ObservedStatusKey, LiveObservedStatus>();
        foreach (var status in statuses)
        {
            ArgumentNullException.ThrowIfNull(status);
            if (status.StatusId == 0)
            {
                continue;
            }

            var key = new ObservedStatusKey(status.StatusId, status.Source?.StableKey);
            current[key] = status;
        }

        statusesByActor.TryGetValue(target.StableKey, out var previous);
        previous ??= new Dictionary<ObservedStatusKey, LiveObservedStatus>();
        var facts = new List<LiveStatusFact>();

        foreach (var pair in previous)
        {
            if (current.ContainsKey(pair.Key))
            {
                continue;
            }

            facts.Add(CreateStatusFact(observedAt, target, pair.Value, applied: false));
        }

        foreach (var pair in current)
        {
            if (!previous.TryGetValue(pair.Key, out var prior))
            {
                facts.Add(CreateStatusFact(observedAt, target, pair.Value, applied: true));
                continue;
            }

            if (prior.Stacks != pair.Value.Stacks)
            {
                facts.Add(CreateStatusFact(observedAt, target, pair.Value, applied: true));
            }
        }

        statusesByActor[target.StableKey] = current;
        return facts;
    }

    public void Reset()
    {
        targetabilityByActor.Clear();
        statusesByActor.Clear();
    }

    private static LiveStatusFact CreateStatusFact(
        DateTimeOffset observedAt,
        LiveActorReference target,
        LiveObservedStatus status,
        bool applied)
    {
        return new LiveStatusFact
        {
            ObservedAt = observedAt,
            Source = status.Source,
            Target = target,
            StatusId = status.StatusId,
            Applied = applied,
            Stacks = status.Stacks,
            Duration = applied ? status.RemainingDuration : null,
            Fidelity = CaptureFidelity.Sampled,
            Confidence = 0.85f,
        };
    }

    private readonly record struct ObservedStatusKey(uint StatusId, string? SourceStableKey);
}
