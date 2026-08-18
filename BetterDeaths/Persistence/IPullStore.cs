namespace BetterDeaths.Persistence;

using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

internal sealed record PullSummary
{
    public required PullId Id { get; init; }

    public required uint TerritoryId { get; init; }

    public required string TerritoryName { get; init; }

    public required TimeSpan Duration { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public required int ActorCount { get; init; }

    public required int EventCount { get; init; }

    public required PullDataSourceKind SourceKind { get; init; }
}

internal sealed record PullQuery
{
    public uint? TerritoryId { get; init; }

    public int Limit { get; init; } = 100;
}

internal interface IPullStore
{
    Task SaveAsync(RecordedPull pull, CancellationToken cancellationToken = default);

    Task<RecordedPull?> LoadAsync(PullId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PullSummary>> QueryAsync(PullQuery query, CancellationToken cancellationToken = default);

    Task DeleteAsync(PullId id, CancellationToken cancellationToken = default);
}
