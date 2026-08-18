namespace BetterDeaths.Domain;

public enum ActorKind
{
    Unknown,
    Player,
    Pet,
    Enemy,
    Npc,
    Object,
}

public sealed record PullMetadata
{
    public required uint TerritoryId { get; init; }

    public required string TerritoryName { get; init; }

    public TimeSpan Duration { get; init; }

    public DateTimeOffset? StartedAt { get; init; }
}

public sealed record ActorRecord
{
    public required ActorId Id { get; init; }

    public required string Name { get; init; }

    public required ActorKind Kind { get; init; }

    public uint? ClassJobId { get; init; }

    public string? JobAbbreviation { get; init; }

    public ActorId? OwnerActorId { get; init; }
}

public sealed record PositionSample
{
    public required long Sequence { get; init; }

    public required TimeSpan PullTime { get; init; }

    public required ActorId ActorId { get; init; }

    public required float X { get; init; }

    public required float Y { get; init; }

    public required float Z { get; init; }

    public float? Rotation { get; init; }

    public required EventProvenance Provenance { get; init; }
}

public sealed record WorldMarkerSample
{
    public required long Sequence { get; init; }

    public required TimeSpan PullTime { get; init; }

    public required int MarkerIndex { get; init; }

    public string? Label { get; init; }

    public required bool Active { get; init; }

    public required float X { get; init; }

    public required float Y { get; init; }

    public required float Z { get; init; }

    public required EventProvenance Provenance { get; init; }
}

public sealed record RecordedPull
{
    public required PullId Id { get; init; }

    public required PullMetadata Metadata { get; init; }

    public required PullSchemaVersion SchemaVersion { get; init; }

    public required IReadOnlyList<ActorRecord> Actors { get; init; }

    public required IReadOnlyList<NormalizedEvent> Events { get; init; }

    public IReadOnlyList<PositionSample> Positions { get; init; } = [];

    public IReadOnlyList<WorldMarkerSample> WorldMarkers { get; init; } = [];

    public required PullProvenance Provenance { get; init; }
}
