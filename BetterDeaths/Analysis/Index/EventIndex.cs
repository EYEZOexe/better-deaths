namespace BetterDeaths.Analysis.Index;

using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class EventIndex
{
    private static readonly IReadOnlyList<NormalizedEvent> EmptyEvents = Array.Empty<NormalizedEvent>();

    private readonly IReadOnlyList<NormalizedEvent> orderedEvents;
    private readonly IReadOnlyDictionary<EventId, NormalizedEvent> eventsById;
    private readonly IReadOnlyDictionary<Type, IReadOnlyList<NormalizedEvent>> eventsByExactType;
    private readonly IReadOnlyDictionary<ActorId, IReadOnlyList<NormalizedEvent>> eventsBySourceActor;
    private readonly IReadOnlyDictionary<ActorId, IReadOnlyList<NormalizedEvent>> eventsByTargetActor;
    private readonly IReadOnlyDictionary<ActorId, IReadOnlyList<NormalizedEvent>> eventsByInvolvedActor;
    private readonly IReadOnlyDictionary<uint, IReadOnlyList<NormalizedEvent>> eventsByActionId;
    private readonly IReadOnlyDictionary<uint, IReadOnlyList<NormalizedEvent>> eventsByStatusId;

    public EventIndex(IReadOnlyList<NormalizedEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var idIndex = new Dictionary<EventId, NormalizedEvent>(events.Count);
        var typeBuckets = new Dictionary<Type, List<NormalizedEvent>>();
        var sourceBuckets = new Dictionary<ActorId, List<NormalizedEvent>>();
        var targetBuckets = new Dictionary<ActorId, List<NormalizedEvent>>();
        var involvedBuckets = new Dictionary<ActorId, List<NormalizedEvent>>();
        var actionBuckets = new Dictionary<uint, List<NormalizedEvent>>();
        var statusBuckets = new Dictionary<uint, List<NormalizedEvent>>();
        var ordered = new NormalizedEvent[events.Count];
        long? previousSequence = null;

        for (var index = 0; index < events.Count; index++)
        {
            var evt = events[index] ?? throw new InvalidOperationException("Canonical event collections cannot contain null entries.");
            if (previousSequence is { } previous && evt.Sequence <= previous)
            {
                throw new InvalidOperationException(
                    $"Canonical event sequence must increase strictly. Previous={previous}, current={evt.Sequence}.");
            }

            if (!idIndex.TryAdd(evt.Id, evt))
            {
                throw new InvalidOperationException($"Duplicate canonical event ID {evt.Id.Value}.");
            }

            ordered[index] = evt;
            previousSequence = evt.Sequence;
            Add(typeBuckets, evt.GetType(), evt);

            if (evt.SourceActorId is { } sourceActorId)
            {
                Add(sourceBuckets, sourceActorId, evt);
                Add(involvedBuckets, sourceActorId, evt);
            }

            if (evt.TargetActorId is { } targetActorId)
            {
                Add(targetBuckets, targetActorId, evt);
                if (evt.SourceActorId != targetActorId)
                {
                    Add(involvedBuckets, targetActorId, evt);
                }
            }

            if (TryGetActionId(evt, out var actionId))
            {
                Add(actionBuckets, actionId, evt);
            }

            if (TryGetStatusId(evt, out var statusId))
            {
                Add(statusBuckets, statusId, evt);
            }
        }

        orderedEvents = ordered;
        eventsById = idIndex;
        eventsByExactType = Freeze(typeBuckets);
        eventsBySourceActor = Freeze(sourceBuckets);
        eventsByTargetActor = Freeze(targetBuckets);
        eventsByInvolvedActor = Freeze(involvedBuckets);
        eventsByActionId = Freeze(actionBuckets);
        eventsByStatusId = Freeze(statusBuckets);
    }

    public IReadOnlyList<NormalizedEvent> All => orderedEvents;

    public bool TryGet(EventId eventId, out NormalizedEvent? evt)
    {
        return eventsById.TryGetValue(eventId, out evt);
    }

    public NormalizedEvent GetRequired(EventId eventId)
    {
        return eventsById.TryGetValue(eventId, out var evt)
            ? evt
            : throw new KeyNotFoundException($"Canonical event ID {eventId.Value} is not present in the pull.");
    }

    public IReadOnlyList<TEvent> OfType<TEvent>()
        where TEvent : NormalizedEvent
    {
        return eventsByExactType.TryGetValue(typeof(TEvent), out var events)
            ? events.Cast<TEvent>().ToArray()
            : Array.Empty<TEvent>();
    }

    public IReadOnlyList<NormalizedEvent> FromActor(ActorId actorId)
    {
        return GetBucket(eventsBySourceActor, actorId);
    }

    public IReadOnlyList<NormalizedEvent> ToActor(ActorId actorId)
    {
        return GetBucket(eventsByTargetActor, actorId);
    }

    public IReadOnlyList<NormalizedEvent> InvolvingActor(ActorId actorId)
    {
        return GetBucket(eventsByInvolvedActor, actorId);
    }

    public IReadOnlyList<NormalizedEvent> ByAction(uint actionId)
    {
        return GetBucket(eventsByActionId, actionId);
    }

    public IReadOnlyList<NormalizedEvent> ByStatus(uint statusId)
    {
        return GetBucket(eventsByStatusId, statusId);
    }

    private static bool TryGetActionId(NormalizedEvent evt, out uint actionId)
    {
        var candidate = evt switch
        {
            DamageEvent damage => damage.ActionId,
            HealEvent heal => heal.ActionId,
            CastStartEvent castStart => castStart.ActionId,
            CastEndEvent castEnd => castEnd.ActionId,
            ActionUseEvent actionUse => actionUse.ActionId,
            RaiseEvent raise => raise.ActionId,
            _ => null,
        };

        if (candidate is { } value)
        {
            actionId = value;
            return true;
        }

        actionId = 0;
        return false;
    }

    private static bool TryGetStatusId(NormalizedEvent evt, out uint statusId)
    {
        switch (evt)
        {
            case StatusApplyEvent statusApply:
                statusId = statusApply.StatusId;
                return true;
            case StatusRemoveEvent statusRemove:
                statusId = statusRemove.StatusId;
                return true;
            default:
                statusId = 0;
                return false;
        }
    }

    private static IReadOnlyList<NormalizedEvent> GetBucket<TKey>(
        IReadOnlyDictionary<TKey, IReadOnlyList<NormalizedEvent>> index,
        TKey key)
        where TKey : notnull
    {
        return index.TryGetValue(key, out var events)
            ? events
            : EmptyEvents;
    }

    private static void Add<TKey>(Dictionary<TKey, List<NormalizedEvent>> index, TKey key, NormalizedEvent evt)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var bucket))
        {
            bucket = [];
            index.Add(key, bucket);
        }

        bucket.Add(evt);
    }

    private static IReadOnlyDictionary<TKey, IReadOnlyList<NormalizedEvent>> Freeze<TKey>(
        Dictionary<TKey, List<NormalizedEvent>> source)
        where TKey : notnull
    {
        return source.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<NormalizedEvent>)pair.Value.ToArray());
    }
}
