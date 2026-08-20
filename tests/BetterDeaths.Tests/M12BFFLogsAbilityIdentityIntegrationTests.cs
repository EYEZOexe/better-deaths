namespace BetterDeaths;

using BetterDeaths.Analysis.Encounters.DancingMadUltimate;
using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Index;
using BetterDeaths.Analysis.Jobs.Dancer;
using BetterDeaths.Domain;
using BetterDeaths.Sources.FFLogs;
using BetterDeaths.Sources.FFLogs.Client;
using BetterDeaths.Windows.Analyzer;
using System.Text.Json;

public sealed class M12BFFLogsAbilityIdentityIntegrationTests
{
    [Fact]
    public async Task CataloguedEncodedDevilmentStatusBecomesTheExactDancerDefinitionIdentity()
    {
        const uint sourceEncodedStatusId = 1_001_825;
        var canonicalStatusId = DancerJobDefinition.Definition
            .Status(DancerJobDefinition.DevilmentStatus)
            .StatusId;
        var normalized = FFLogsEventNormalizer.Normalize(
            Import(
                abilities: [Ability(sourceEncodedStatusId)],
                actors:
                [
                    Actor(10, "Synthetic Dancer", "Player", "Dancer"),
                    Actor(30, "Synthetic Target", "NPC", "Boss"),
                ],
                events:
                [
                    Event(
                        2_000,
                        "applybuff",
                        """{"sourceID":10,"targetID":10,"abilityGameID":1001825,"duration":20000}"""),
                    Event(
                        22_000,
                        "removebuff",
                        """{"sourceID":10,"targetID":10,"abilityGameID":1001825}"""),
                ]),
            new PullSchemaVersion(1));

        Assert.Empty(normalized.SkippedEvents);
        Assert.Empty(normalized.AbilityIdentityDiagnostics);
        Assert.Empty(normalized.StatusDurationDiagnostics);
        Assert.Equal((uint)1_825, canonicalStatusId);
        Assert.NotEqual(sourceEncodedStatusId, canonicalStatusId);
        var actor = Assert.Single(normalized.Pull.Actors, candidate => candidate.Kind == ActorKind.Player);
        var apply = Assert.Single(normalized.Pull.Events.OfType<StatusApplyEvent>());
        var remove = Assert.Single(normalized.Pull.Events.OfType<StatusRemoveEvent>());
        Assert.Equal(canonicalStatusId, apply.StatusId);
        Assert.Equal(canonicalStatusId, remove.StatusId);
        Assert.Equal(TimeSpan.FromSeconds(20), apply.Duration);

        var index = new StatusIntervalIndex(
            new EventIndex(normalized.Pull.Events),
            normalized.Pull.Metadata.Duration);
        Assert.Empty(index.ForActorStatus(actor.Id, sourceEncodedStatusId));
        Assert.Single(index.ForActorStatus(actor.Id, canonicalStatusId));

        var run = await AnalyzerWorkspaceEngineComposition.CreateDefault().AnalyzeAsync(normalized.Pull);
        Assert.Empty(run.Failures);
        Assert.DoesNotContain(
            run.Skipped,
            skip => skip.AnalyzerId is DancerCoreExecutionAnalyzer.AnalyzerId or
                DancerBurstAndUptimeAnalyzer.AnalyzerId);
    }

    [Fact]
    public async Task CataloguedForsakenStatusesNormalizeBeforeAnalysisAndKeepLocalParity()
    {
        var actors = new[]
        {
            Actor(1, "Tank One", "Player", "PLD"),
            Actor(2, "Tank Two", "Player", "WAR"),
            Actor(3, "Healer One", "Player", "WHM"),
            Actor(4, "Healer Two", "Player", "SCH"),
            Actor(5, "Melee One", "Player", "DRG"),
            Actor(6, "Melee Two", "Player", "VPR"),
            Actor(7, "Ranged One", "Player", "BRD"),
            Actor(8, "Ranged Two", "Player", "PCT"),
            Actor(30, "Synthetic Boss", "NPC", "Boss"),
        };
        var encodedStatuses = new uint[]
        {
            1_005_086,
            1_005_084,
            1_005_086,
            1_005_085,
            1_005_085,
            1_005_084,
            1_005_085,
            1_005_086,
        };
        var events = encodedStatuses
            .Select((statusId, index) => Event(
                10_000 + index * 100,
                "applydebuff",
                $$"""{"sourceID":30,"targetID":{{index + 1}},"abilityGameID":{{statusId}},"duration":9999000}"""))
            .ToArray();
        var normalized = FFLogsEventNormalizer.Normalize(
            Import(
                abilities: [Ability(1_005_084), Ability(1_005_085), Ability(1_005_086)],
                actors: actors,
                events: events),
            new PullSchemaVersion(1));

        Assert.Empty(normalized.SkippedEvents);
        Assert.Empty(normalized.AbilityIdentityDiagnostics);
        Assert.Equal(8, normalized.StatusDurationDiagnostics.Count);
        Assert.All(
            normalized.StatusDurationDiagnostics,
            diagnostic => Assert.Equal(
                FFLogsStatusDurationClassification.IndefiniteSentinelUnavailable,
                diagnostic.Classification));
        var statuses = normalized.Pull.Events.OfType<StatusApplyEvent>().ToArray();
        Assert.Equal(
            new uint[] { 5_086, 5_084, 5_086, 5_085, 5_085, 5_084, 5_085, 5_086 },
            statuses.Select(status => status.StatusId));
        Assert.All(statuses, status => Assert.Null(status.Duration));

        var registry = new AnalyzerRegistry();
        registry.Register(new ForsakenOpeningAssignmentAnalyzer());
        var engine = new AnalyzerEngine(registry);
        var sourceRun = await engine.AnalyzeAsync(normalized.Pull);
        var localRun = await engine.AnalyzeAsync(AsEquivalentLocalPull(normalized.Pull));

        Assert.Empty(sourceRun.Failures);
        Assert.Empty(sourceRun.Skipped);
        Assert.Equal(4, sourceRun.Results.Count);
        Assert.Equal(Project(sourceRun.Results), Project(localRun.Results));
    }

    private static FFLogsFightImportData Import(
        IReadOnlyList<FFLogsReportAbility> abilities,
        IReadOnlyList<FFLogsReportActor> actors,
        IReadOnlyList<FFLogsEventEnvelope> events)
    {
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
                Code = "M12BTEST",
                StartTimeUnixMilliseconds = 0,
                EndTimeUnixMilliseconds = 60_000,
                Revision = 1,
            },
            Fights = [fight],
            Actors = actors,
            Abilities = abilities,
        };

        return new FFLogsFightImportData
        {
            ReportDocument = report,
            Fight = fight,
            Events = events,
            Actors = actors,
        };
    }

    private static FFLogsReportActor Actor(int id, string name, string type, string subType)
    {
        return new FFLogsReportActor
        {
            Id = id,
            Name = name,
            Type = type,
            SubType = subType,
        };
    }

    private static FFLogsReportAbility Ability(uint gameId)
    {
        return new FFLogsReportAbility
        {
            GameId = gameId,
            Name = $"Ability {gameId}",
            Icon = "synthetic-icon.png",
            Type = "Synthetic Type",
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

    private static RecordedPull AsEquivalentLocalPull(RecordedPull sourcePull)
    {
        var eventProvenance = new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = "local:m12b-parity",
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
                SourceReference = "local:m12b-parity",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static IReadOnlyList<ResultProjection> Project(IReadOnlyList<AnalysisResult> results)
    {
        return results.Select(result => new ResultProjection(
            result.AnalyzerId,
            result.RuleKey,
            result.Severity,
            result.Category,
            result.TimeRange,
            string.Join(",", result.Actors.Select(actor => actor.Value)),
            result.Confidence,
            string.Join(",", result.Metrics.OrderBy(metric => metric.Key).Select(metric => $"{metric.Key}={metric.Value:R}")),
            string.Join(",", result.Evidence.SelectMany(evidence => evidence.EventIds).Select(id => id.Value))))
            .ToArray();
    }

    private sealed record ResultProjection(
        string AnalyzerId,
        string? RuleKey,
        AnalysisSeverity Severity,
        AnalysisCategory Category,
        TimeRange? TimeRange,
        string Actors,
        float Confidence,
        string Metrics,
        string EvidenceEventIds);
}
