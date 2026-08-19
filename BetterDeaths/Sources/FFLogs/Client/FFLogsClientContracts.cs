namespace BetterDeaths.Sources.FFLogs.Client;

using BetterDeaths.Sources;
using System;
using System.Collections.Generic;

internal sealed record FFLogsApiClientOptions
{
    public static Uri DefaultPublicGraphQlEndpoint { get; } = new("https://www.fflogs.com/api/v2/client");

    public static Uri DefaultUserGraphQlEndpoint { get; } = new("https://www.fflogs.com/api/v2/user");

    public Uri PublicGraphQlEndpoint { get; init; } = DefaultPublicGraphQlEndpoint;

    public Uri UserGraphQlEndpoint { get; init; } = DefaultUserGraphQlEndpoint;

    public int EventPageLimit { get; init; } = 10000;

    public TimeSpan ReportMetadataCacheDuration { get; init; } = TimeSpan.FromMinutes(2);

    public void Validate()
    {
        ValidateEndpoint(PublicGraphQlEndpoint, nameof(PublicGraphQlEndpoint));
        ValidateEndpoint(UserGraphQlEndpoint, nameof(UserGraphQlEndpoint));
        if (EventPageLimit is < 100 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(EventPageLimit), "FFLogs event page limit must be between 100 and 10000.");
        }

        if (ReportMetadataCacheDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ReportMetadataCacheDuration));
        }
    }

    public Uri GetGraphQlEndpoint(FFLogsApiAccessKind accessKind)
    {
        return accessKind switch
        {
            FFLogsApiAccessKind.PublicClient => PublicGraphQlEndpoint,
            FFLogsApiAccessKind.UserAuthorized => UserGraphQlEndpoint,
            _ => throw new ArgumentOutOfRangeException(nameof(accessKind)),
        };
    }

    private static void ValidateEndpoint(Uri endpoint, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("FFLogs API endpoints must be absolute HTTPS URIs.", parameterName);
        }
    }
}

internal sealed record FFLogsReportDocument
{
    public required FFLogsReportMetadata Report { get; init; }

    public required IReadOnlyList<FFLogsFightMetadata> Fights { get; init; }
}

internal sealed record FFLogsEventPage
{
    public required IReadOnlyList<FFLogsEventEnvelope> Events { get; init; }

    public double? NextPageTimestampMilliseconds { get; init; }
}

internal sealed record FFLogsFightImportData
{
    public required FFLogsReportDocument ReportDocument { get; init; }

    public required FFLogsFightMetadata Fight { get; init; }

    public required IReadOnlyList<FFLogsEventEnvelope> Events { get; init; }
}

internal sealed record FFLogsApiResult<T>
    where T : class
{
    private FFLogsApiResult(T? value, PullImportError? error)
    {
        if ((value is null) == (error is null))
        {
            throw new ArgumentException("An FFLogs API result must contain exactly one of Value or Error.");
        }

        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public PullImportError? Error { get; }

    public bool IsSuccess => Value is not null;

    public static FFLogsApiResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new FFLogsApiResult<T>(value, null);
    }

    public static FFLogsApiResult<T> Failure(PullImportError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new FFLogsApiResult<T>(null, error);
    }
}

internal sealed class FFLogsIntegrationException : Exception
{
    public FFLogsIntegrationException(PullImportError error)
        : base(error?.SafeMessage)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public PullImportError Error { get; }
}
