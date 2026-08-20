namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Jobs.Dancer;
using BetterDeaths.Domain;
using BetterDeaths.Sources.FFLogs;
using BetterDeaths.Sources.FFLogs.Client;
using BetterDeaths.Windows.Analyzer;
using System.Text.Json;

public sealed class M12AFFLogsJobIdentityIntegrationTests
{
    [Fact]
    public async Task SourceDancerIdentityActivatesBothDefaultModulesWithLocalParity()
    {
        var normalized = FFLogsEventNormalizer.Normalize(
            Import(
                "Dancer",
                Event(
                    2_000,
                    "cast",
                    """{"sourceID":10,"targetID":30,"abilityGameID":15997}"""),
                Event(
                    3_000,
                    "cast",
                    """{"sourceID":10,"targetID":30,"abilityGameID":16191}""")),
            new PullSchemaVersion(1));

        Assert.Empty(normalized.SkippedEvents);
        var sourceActor = Assert.Single(normalized.Pull.Actors, actor => actor.Kind == ActorKind.Player);
        Assert.Equal(DancerJobDefinition.JobAbbreviation, sourceActor.JobAbbreviation);

        var engine = AnalyzerWorkspaceEngineComposition.CreateDefault();
        var sourceRun = await engine.AnalyzeAsync(normalized.Pull);

        Assert.Empty(sourceRun.Failures);
        Assert.DoesNotContain(sourceRun.Skipped, skip => IsDancerAnalyzer(skip.AnalyzerId));
        Assert.Contains(
            sourceRun.Results,
            result => result.AnalyzerId == DancerCoreExecutionAnalyzer.AnalyzerId);

        var localPull = AsEquivalentLocalPull(normalized.Pull);
        var localRun = await engine.AnalyzeAsync(localPull);

        Assert.Empty(localRun.Failures);
        Assert.DoesNotContain(localRun.Skipped, skip => IsDancerAnalyzer(skip.AnalyzerId));
        Assert.Equal(ProjectDancerResults(sourceRun.Results), ProjectDancerResults(localRun.Results));
    }

    [Fact]
    public async Task UnknownPlayerJobLeavesBothDefaultDancerModulesUnsupported()
    {
        var normalized = FFLogsEventNormalizer.Normalize(
            Import(
                "DancerMain",
                Event(
                    2_000,
                    "cast",
                    """{"sourceID":10,"targetID":30,"abilityGameID":15997}"""),
                Event(
                    3_000,
                    "cast",
                    """{"sourceID":10,"targetID":30,"abilityGameID":16191}""")),
            new PullSchemaVersion(1));

        Assert.Empty(normalized.SkippedEvents);
        var sourceActor = Assert.Single(normalized.Pull.Actors, actor => actor.Kind == ActorKind.Player);
        Assert.Null(sourceActor.JobAbbreviation);

        var run = await AnalyzerWorkspaceEngineComposition.CreateDefault().AnalyzeAsync(normalized.Pull);
        var dancerSkips = run.Skipped
            .Where(skip => IsDancerAnalyzer(skip.AnalyzerId))
            .OrderBy(skip => skip.AnalyzerId, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(run.Failures);
        Assert.Equal(2, dancerSkips.Length);
        Assert.All(dancerSkips, skip => Assert.Equal(AnalyzerSkipReason.Unsupported, skip.Reason));
        Assert.Equal(
            [DancerBurstAndUptimeAnalyzer.AnalyzerId, DancerCoreExecutionAnalyzer.AnalyzerId],
            dancerSkips.Select(skip => skip.AnalyzerId));
        Assert.DoesNotContain(run.Results, result => IsDancerAnalyzer(result.AnalyzerId));
    }

    private static bool IsDancerAnalyzer(string analyzerId)
    {
        return analyzerId is DancerCoreExecutionAnalyzer.AnalyzerId or
            DancerBurstAndUptimeAnalyzer.AnalyzerId;
    }

    private static RecordedPull AsEquivalentLocalPull(RecordedPull sourcePull)
    {
        var eventProvenance = new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = "local:m12a-parity",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };

        return sourcePull with
        {
            Events = sourcePull.Events
                .Select(evt => evt with { Provenance = eventProvenance })
                .ToArray(),
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = "local:m12a-parity",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static IReadOnlyList<DancerResultProjection> ProjectDancerResults(
        IReadOnlyList<AnalysisResult> results)
    {
        return results
            .Where(result => IsDancerAnalyzer(result.AnalyzerId))
            .Select(result => new DancerResultProjection(
                result.AnalyzerId,
                result.RuleKey,
                result.Severity,
                result.Category,
                result.Title,
                result.Summary,
                result.TimeRange,
                string.Join(",", result.Actors.Select(actor => actor.Value)),
                result.Confidence,
                string.Join(",", result.Metrics
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}={pair.Value:R}")),
                string.Join(",", result.Evidence
                    .SelectMany(evidence => evidence.EventIds)
                    .Select(id => id.Value))))
            .ToArray();
    }

    private static FFLogsFightImportData Import(
        string playerSubType,
        params FFLogsEventEnvelope[] events)
    {
        var actors = new FFLogsReportActor[]
        {
            new()
            {
                Id = 10,
                Name = "Synthetic Player",
                Type = "Player",
                SubType = playerSubType,
            },
            new()
            {
                Id = 30,
                Name = "Synthetic Target",
                Type = "NPC",
                SubType = "Boss",
            },
        };
        var fight = new FFLogsFightMetadata
        {
            Id = 3,
            EncounterId = 999,
            Name = "Synthetic Encounter",
            StartTimeMilliseconds = 1_000,
            EndTimeMilliseconds = 60_000,
            GameZoneId = 1_363,
            GameZoneName = "Synthetic Zone",
        };
        var report = new FFLogsReportDocument
        {
            Report = new FFLogsReportMetadata
            {
                Code = "M12ATEST",
                StartTimeUnixMilliseconds = 1_700_000_000_000,
                EndTimeUnixMilliseconds = 1_700_000_060_000,
                Revision = 1,
            },
            Fights = [fight],
            Actors = actors,
        };

        return new FFLogsFightImportData
        {
            Profile = FFLogsImportProfile.Core,
            ReportDocument = report,
            Fight = fight,
            Events = events,
            Actors = actors,
        };
    }

    private static FFLogsEventEnvelope Event(double timestampMilliseconds, string type, string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return new FFLogsEventEnvelope
        {
            TimestampMilliseconds = timestampMilliseconds,
            Type = type,
            Payload = document.RootElement.Clone(),
        };
    }

    private sealed record DancerResultProjection(
        string AnalyzerId,
        string? RuleKey,
        AnalysisSeverity Severity,
        AnalysisCategory Category,
        string Title,
        string Summary,
        TimeRange? TimeRange,
        string Actors,
        float Confidence,
        string Metrics,
        string EvidenceEventIds);
}
