namespace BetterDeaths.Sources;

using BetterDeaths.Domain;
using System;
using System.Threading;
using System.Threading.Tasks;

internal abstract record PullSourceRequest;

public enum PullImportErrorCategory
{
    InvalidRequest,
    Authentication,
    Authorization,
    NotFound,
    RateLimited,
    Network,
    Protocol,
    Unavailable,
}

internal sealed record PullImportError
{
    public required PullImportErrorCategory Category { get; init; }

    public required string Code { get; init; }

    public required string SafeMessage { get; init; }

    public TimeSpan? RetryAfter { get; init; }
}

internal sealed record PullImportResult
{
    private PullImportResult(RecordedPull? pull, PullImportError? error)
    {
        if ((pull is null) == (error is null))
        {
            throw new ArgumentException("A pull import result must contain exactly one of Pull or Error.");
        }

        Pull = pull;
        Error = error;
    }

    public RecordedPull? Pull { get; }

    public PullImportError? Error { get; }

    public bool IsSuccess => Pull is not null;

    public static PullImportResult Success(RecordedPull pull)
    {
        ArgumentNullException.ThrowIfNull(pull);
        return new PullImportResult(pull, null);
    }

    public static PullImportResult Failure(PullImportError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new PullImportResult(null, error);
    }
}

internal interface IPullDataSource
{
    ValueTask<PullImportResult> LoadAsync(
        PullSourceRequest request,
        CancellationToken cancellationToken = default);
}
