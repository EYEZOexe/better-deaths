namespace BetterDeaths.Sources.FFLogs;

using BetterDeaths.Domain;
using BetterDeaths.Sources.FFLogs.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed record FFLogsReportSelection
{
    public required string ReportCode { get; init; }

    public required IReadOnlyList<FFLogsFightSelection> Fights { get; init; }
}

internal sealed record FFLogsFightSelection
{
    public required int FightId { get; init; }

    public required string Name { get; init; }

    public required TimeSpan Start { get; init; }

    public required TimeSpan Duration { get; init; }

    public bool? Kill { get; init; }
}

internal sealed record FFLogsReportSelectionResult
{
    private FFLogsReportSelectionResult(FFLogsReportSelection? report, PullImportError? error)
    {
        if ((report is null) == (error is null))
        {
            throw new ArgumentException("An FFLogs report selection result must contain exactly one report or error.");
        }

        Report = report;
        Error = error;
    }

    public FFLogsReportSelection? Report { get; }

    public PullImportError? Error { get; }

    public bool IsSuccess => Report is not null;

    public static FFLogsReportSelectionResult Success(FFLogsReportSelection report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new FFLogsReportSelectionResult(report, null);
    }

    public static FFLogsReportSelectionResult Failure(PullImportError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new FFLogsReportSelectionResult(null, error);
    }
}

internal interface IFFLogsImportSource
{
    ValueTask<FFLogsReportSelectionResult> LoadReportSelectionAsync(
        string reportCode,
        CancellationToken cancellationToken = default);

    ValueTask<PullImportResult> LoadPullAsync(
        string reportCode,
        int fightId,
        CancellationToken cancellationToken = default);
}

internal sealed class FFLogsPullSource : IFFLogsImportSource
{
    private readonly FFLogsGraphQlClient client;
    private readonly PullSchemaVersion schemaVersion;
    private readonly FFLogsApiAccessKind accessKind;

    public FFLogsPullSource(
        FFLogsGraphQlClient client,
        PullSchemaVersion schemaVersion,
        FFLogsApiAccessKind accessKind = FFLogsApiAccessKind.PublicClient)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
        this.schemaVersion = schemaVersion;
        this.accessKind = accessKind;
    }

    public async ValueTask<FFLogsReportSelectionResult> LoadReportSelectionAsync(
        string reportCode,
        CancellationToken cancellationToken = default)
    {
        var result = await client
            .LoadReportAsync(reportCode, accessKind, cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return FFLogsReportSelectionResult.Failure(result.Error!);
        }

        var document = result.Value!;
        var fights = document.Fights
            .OrderBy(fight => fight.StartTimeMilliseconds)
            .ThenBy(fight => fight.Id)
            .Select(fight => new FFLogsFightSelection
            {
                FightId = fight.Id,
                Name = string.IsNullOrWhiteSpace(fight.Name) ? $"Fight {fight.Id}" : fight.Name.Trim(),
                Start = TimeSpan.FromMilliseconds(Math.Max(0.0, fight.StartTimeMilliseconds)),
                Duration = TimeSpan.FromMilliseconds(Math.Max(0.0, fight.EndTimeMilliseconds - fight.StartTimeMilliseconds)),
                Kill = fight.Kill,
            })
            .ToArray();
        return FFLogsReportSelectionResult.Success(new FFLogsReportSelection
        {
            ReportCode = document.Report.Code,
            Fights = fights,
        });
    }

    public async ValueTask<PullImportResult> LoadPullAsync(
        string reportCode,
        int fightId,
        CancellationToken cancellationToken = default)
    {
        var result = await client
            .LoadFightAsync(reportCode, fightId, accessKind, cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return PullImportResult.Failure(result.Error!);
        }

        try
        {
            var normalized = FFLogsEventNormalizer.Normalize(result.Value!, schemaVersion);
            return PullImportResult.Success(normalized.Pull);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return PullImportResult.Failure(FFLogsIntegrationErrors.ProtocolFailure(FFLogsOperation.LoadFight));
        }
    }
}
