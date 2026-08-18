namespace BetterDeaths.Capture.FullPull;

using BetterDeaths.Domain;
using System;

internal sealed record LiveActorReference
{
    public required string StableKey { get; init; }

    public required string Name { get; init; }

    public required ActorKind Kind { get; init; }

    public uint? ClassJobId { get; init; }

    public string? JobAbbreviation { get; init; }

    public LiveActorReference? Owner { get; init; }
}

internal abstract record LiveCaptureFact
{
    public required DateTimeOffset ObservedAt { get; init; }

    public CaptureFidelity Fidelity { get; init; } = CaptureFidelity.Exact;

    public float Confidence { get; init; } = 1.0f;
}

internal enum LiveActionEffectKind
{
    Damage,
    Heal,
    ActionUse,
}

internal sealed record LiveActionEffectFact : LiveCaptureFact
{
    public required LiveActionEffectKind Kind { get; init; }

    public LiveActorReference? Source { get; init; }

    public LiveActorReference? Target { get; init; }

    public required uint ActionId { get; init; }

    public long Amount { get; init; }

    public bool IsCritical { get; init; }

    public bool IsDirectHit { get; init; }
}

internal sealed record LiveStatusFact : LiveCaptureFact
{
    public LiveActorReference? Source { get; init; }

    public required LiveActorReference Target { get; init; }

    public required uint StatusId { get; init; }

    public required bool Applied { get; init; }

    public ushort Stacks { get; init; }

    public TimeSpan? Duration { get; init; }
}

internal sealed record LiveDeathFact : LiveCaptureFact
{
    public LiveActorReference? Source { get; init; }

    public required LiveActorReference Target { get; init; }
}

internal sealed record LiveRaiseFact : LiveCaptureFact
{
    public LiveActorReference? Source { get; init; }

    public required LiveActorReference Target { get; init; }

    public uint? ActionId { get; init; }
}

internal sealed record LiveTargetabilityFact : LiveCaptureFact
{
    public required LiveActorReference Actor { get; init; }

    public required bool IsTargetable { get; init; }
}

internal sealed record LiveGaugeFact : LiveCaptureFact
{
    public required LiveActorReference Actor { get; init; }

    public required string GaugeKey { get; init; }

    public required double Value { get; init; }
}

internal sealed record LiveTetherFact : LiveCaptureFact
{
    public required LiveActorReference Source { get; init; }

    public required LiveActorReference Target { get; init; }

    public required uint TetherId { get; init; }

    public bool Active { get; init; } = true;
}

internal sealed record LiveMarkerFact : LiveCaptureFact
{
    public required LiveActorReference Target { get; init; }

    public required uint MarkerId { get; init; }

    public bool Active { get; init; } = true;
}

internal sealed record LiveMechanicSignalFact : LiveCaptureFact
{
    public LiveActorReference? Source { get; init; }

    public required string SignalKey { get; init; }

    public uint? SignalId { get; init; }

    public long? State { get; init; }
}

internal sealed record LivePositionFact : LiveCaptureFact
{
    public required LiveActorReference Actor { get; init; }

    public required float X { get; init; }

    public required float Y { get; init; }

    public required float Z { get; init; }

    public float? Rotation { get; init; }
}

internal sealed record LiveWorldMarkerFact : LiveCaptureFact
{
    public required int MarkerIndex { get; init; }

    public string? Label { get; init; }

    public required bool Active { get; init; }

    public required float X { get; init; }

    public required float Y { get; init; }

    public required float Z { get; init; }
}
