namespace BetterDeaths.Sources.FFLogs;

using BetterDeaths.Sources;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal enum FFLogsApiAccessKind
{
    PublicClient,
    UserAuthorized,
}

internal sealed record FFLogsPullSourceRequest : PullSourceRequest
{
    public required string ReportCode { get; init; }

    public required int FightId { get; init; }

    public FFLogsApiAccessKind AccessKind { get; init; } = FFLogsApiAccessKind.PublicClient;

    public void Validate()
    {
        FFLogsSourceReference.Validate(ReportCode, FightId);
    }
}

internal sealed record FFLogsReportMetadata
{
    public required string Code { get; init; }

    // The FFLogs GraphQL schema exposes report timestamps as Float milliseconds.
    public required double StartTimeUnixMilliseconds { get; init; }

    public required double EndTimeUnixMilliseconds { get; init; }

    public required int Revision { get; init; }
}

internal sealed record FFLogsReportActor
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Type { get; init; }

    public string? SubType { get; init; }

    public int? PetOwnerId { get; init; }
}

internal sealed record FFLogsReportAbility
{
    public required uint GameId { get; init; }

    public required string Name { get; init; }

    public required string Icon { get; init; }

    public required string Type { get; init; }
}

internal sealed record FFLogsFightMetadata
{
    public required int Id { get; init; }

    public required int EncounterId { get; init; }

    public required string Name { get; init; }

    public required double StartTimeMilliseconds { get; init; }

    public required double EndTimeMilliseconds { get; init; }

    public bool? Kill { get; init; }

    public bool? InProgress { get; init; }

    // GameZone.id is currently exposed as GraphQL Float; normalization validates/converts later.
    public double? GameZoneId { get; init; }

    public string? GameZoneName { get; init; }
}

internal sealed record FFLogsEventEnvelope
{
    public required double TimestampMilliseconds { get; init; }

    public required string Type { get; init; }

    public required JsonElement Payload { get; init; }
}

internal sealed class FFLogsAccessToken
{
    private readonly string value;

    public FFLogsAccessToken(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        this.value = value;
    }

    internal string RevealForAuthorizationHeader()
    {
        return value;
    }

    public override string ToString()
    {
        return "[REDACTED FFLogs access token]";
    }
}

internal interface IFFLogsAccessTokenProvider
{
    ValueTask<FFLogsAccessToken> GetAccessTokenAsync(
        FFLogsApiAccessKind accessKind,
        CancellationToken cancellationToken = default);
}
