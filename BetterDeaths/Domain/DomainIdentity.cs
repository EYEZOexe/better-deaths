namespace BetterDeaths.Domain;

using System;

public readonly record struct PullId(Guid Value)
{
    public static PullId New() => new(Guid.NewGuid());
}

public readonly record struct EventId(long Value);

public readonly record struct ActorId(int Value);

public readonly record struct AnalysisResultId(Guid Value)
{
    public static AnalysisResultId New() => new(Guid.NewGuid());
}

public readonly record struct PullSchemaVersion(int Value);
