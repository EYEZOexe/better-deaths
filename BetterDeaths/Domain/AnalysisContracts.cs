namespace BetterDeaths.Domain;

using System;
using System.Collections.Generic;

public readonly record struct TimeRange(TimeSpan Start, TimeSpan End);

public enum AnalysisSeverity
{
    Info,
    Observation,
    Optimization,
    Warning,
    Error,
    Critical,
}

public enum AnalysisCategory
{
    Death,
    Mitigation,
    Healing,
    Damage,
    Uptime,
    Raise,
    Buff,
    Cooldown,
    Movement,
    Job,
    Mechanic,
    Session,
    DataQuality,
}

public sealed record AnalysisEvidence
{
    public IReadOnlyList<EventId> EventIds { get; init; } = [];

    public IReadOnlyList<ActorId> ActorIds { get; init; } = [];

    public TimeRange? TimeRange { get; init; }

    public string? Explanation { get; init; }
}

public sealed record AnalysisResult
{
    public required AnalysisResultId Id { get; init; }

    public required string AnalyzerId { get; init; }

    public required AnalysisSeverity Severity { get; init; }

    public required AnalysisCategory Category { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public TimeRange? TimeRange { get; init; }

    public IReadOnlyList<ActorId> Actors { get; init; } = [];

    public required IReadOnlyList<AnalysisEvidence> Evidence { get; init; }

    public float Confidence { get; init; } = 1.0f;

    public IReadOnlyDictionary<string, double> Metrics { get; init; } = new Dictionary<string, double>();
}
