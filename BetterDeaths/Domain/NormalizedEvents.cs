namespace BetterDeaths.Domain;

using System;
using System.Text.Json.Serialization;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$eventType")]
[JsonDerivedType(typeof(DamageEvent), "damage")]
[JsonDerivedType(typeof(HealEvent), "heal")]
[JsonDerivedType(typeof(CastStartEvent), "cast-start")]
[JsonDerivedType(typeof(CastEndEvent), "cast-end")]
[JsonDerivedType(typeof(ActionUseEvent), "action-use")]
[JsonDerivedType(typeof(StatusApplyEvent), "status-apply")]
[JsonDerivedType(typeof(StatusRemoveEvent), "status-remove")]
[JsonDerivedType(typeof(DeathEvent), "death")]
[JsonDerivedType(typeof(RaiseEvent), "raise")]
[JsonDerivedType(typeof(TargetabilityEvent), "targetability")]
[JsonDerivedType(typeof(GaugeEvent), "gauge")]
[JsonDerivedType(typeof(TetherEvent), "tether")]
[JsonDerivedType(typeof(MarkerEvent), "marker")]
[JsonDerivedType(typeof(MechanicSignalEvent), "mechanic-signal")]
public abstract record NormalizedEvent
{
    public required EventId Id { get; init; }

    public required long Sequence { get; init; }

    public required TimeSpan PullTime { get; init; }

    public DateTimeOffset? ObservedAt { get; init; }

    public ActorId? SourceActorId { get; init; }

    public ActorId? TargetActorId { get; init; }

    public required EventProvenance Provenance { get; init; }
}

public sealed record DamageEvent : NormalizedEvent
{
    public required long Amount { get; init; }

    public uint? ActionId { get; init; }

    public bool IsCritical { get; init; }

    public bool IsDirectHit { get; init; }
}

public sealed record HealEvent : NormalizedEvent
{
    public required long Amount { get; init; }

    public uint? ActionId { get; init; }
}

public sealed record CastStartEvent : NormalizedEvent
{
    public required uint ActionId { get; init; }

    public required TimeSpan CastDuration { get; init; }
}

public sealed record CastEndEvent : NormalizedEvent
{
    public required uint ActionId { get; init; }

    public bool Interrupted { get; init; }
}

public sealed record ActionUseEvent : NormalizedEvent
{
    public required uint ActionId { get; init; }
}

public sealed record StatusApplyEvent : NormalizedEvent
{
    public required uint StatusId { get; init; }

    public ushort Stacks { get; init; }

    public TimeSpan? Duration { get; init; }
}

public sealed record StatusRemoveEvent : NormalizedEvent
{
    public required uint StatusId { get; init; }
}

public sealed record DeathEvent : NormalizedEvent;

public sealed record RaiseEvent : NormalizedEvent
{
    public uint? ActionId { get; init; }
}

public sealed record TargetabilityEvent : NormalizedEvent
{
    public required bool IsTargetable { get; init; }
}

public sealed record GaugeEvent : NormalizedEvent
{
    public required string GaugeKey { get; init; }

    public required double Value { get; init; }
}

public sealed record TetherEvent : NormalizedEvent
{
    public required uint TetherId { get; init; }

    public bool Active { get; init; } = true;
}

public sealed record MarkerEvent : NormalizedEvent
{
    public required uint MarkerId { get; init; }

    public bool Active { get; init; } = true;
}

public sealed record MechanicSignalEvent : NormalizedEvent
{
    public required string SignalKey { get; init; }

    public uint? SignalId { get; init; }

    public long? State { get; init; }
}
