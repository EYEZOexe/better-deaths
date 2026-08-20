namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Generic;
using BetterDeaths.Domain;
using BetterDeaths.Sources.FFLogs;
using BetterDeaths.Sources.FFLogs.Client;
using System.Text.Json;

public sealed class FFLogsAnalyzerParityTests
{
    private static readonly ActorId Player = new(1);
    private static readonly ActorId Boss = new(2);

    [Fact]
    public async Task EquivalentLocalAndFFLogsFactsProduceEquivalentGenericAnalysisSemantics()
    {
        var fflogsPull = FFLogsEventNormalizer.Normalize(CreateFFLogsFixture(), new PullSchemaVersion(1)).Pull;
        var localPull = CreateEquivalentLocalPull();
        var engine = CreateEngine();

        var localRun = await engine.AnalyzeAsync(localPull);
        var importedRun = await engine.AnalyzeAsync(fflogsPull);

        Assert.Empty(localRun.Failures);
        Assert.Empty(importedRun.Failures);
        Assert.Equal(localRun.Skipped.Select(skip => (skip.AnalyzerId, skip.Reason)), importedRun.Skipped.Select(skip => (skip.AnalyzerId, skip.Reason)));

        var local = ProjectResults(localRun.Results);
        var imported = ProjectResults(importedRun.Results);
        Assert.Equal(local, imported);

        Assert.All(localPull.Events, evt => Assert.Equal(PullDataSourceKind.DalamudLive, evt.Provenance.SourceKind));
        Assert.All(fflogsPull.Events, evt => Assert.Equal(PullDataSourceKind.FFLogs, evt.Provenance.SourceKind));
    }

    [Fact]
    public async Task MissingFFLogsEvidenceStaysMissingInsteadOfChangingAnalyzerMeaning()
    {
        var import = CreateFFLogsFixture() with
        {
            Events =
            [
                Event(6000, "damage", """{"sourceID":30,"targetID":10,"abilityGameID":100,"amount":12000}"""),
                Event(12000, "death", """{"sourceID":30,"targetID":10}"""),
                Event(13000, "mystery", """{"sourceID":10,"targetID":10,"amount":99999}"""),
            ],
        };
        var normalized = FFLogsEventNormalizer.Normalize(import, new PullSchemaVersion(1));
        var engine = CreateEngine();

        var run = await engine.AnalyzeAsync(normalized.Pull);

        var skippedSourceFact = Assert.Single(normalized.SkippedEvents);
        Assert.Contains("unsupported", skippedSourceFact.Reason, StringComparison.Ordinal);
        var death = Assert.Single(run.Results, result => result.AnalyzerId == DeathRaiseContextAnalyzer.AnalyzerId);
        Assert.Contains("does not label any nearby hit as lethal or as a player mistake", death.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(run.Results, result => result.Summary.Contains("99999", StringComparison.Ordinal));
    }

    [Fact]
    public void NormalizationFailureIsADataSourceConcernNotAnAnalyzerFailure()
    {
        var invalid = CreateFFLogsFixture() with
        {
            Fight = CreateFight() with
            {
                StartTimeMilliseconds = 20_000,
                EndTimeMilliseconds = 10_000,
            },
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            FFLogsEventNormalizer.Normalize(invalid, new PullSchemaVersion(1)));

        Assert.Contains("end time precedes", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SameCanonicalPullAnalyzesIdenticallyAfterPersistenceRoundTripRegardlessOfFFLogsOrigin()
    {
        var pull = FFLogsEventNormalizer.Normalize(CreateFFLogsFixture(), new PullSchemaVersion(1)).Pull;
        var json = BetterDeaths.Persistence.CanonicalPullSerializer.Serialize(pull);
        var reloaded = BetterDeaths.Persistence.CanonicalPullSerializer.Deserialize(json);
        var engine = CreateEngine();

        var before = await engine.AnalyzeAsync(pull);
        var after = await engine.AnalyzeAsync(reloaded);

        Assert.Equal(ProjectResults(before.Results), ProjectResults(after.Results));
        Assert.Equal(pull.Provenance, reloaded.Provenance);
        Assert.Equal(pull.Events.Select(evt => evt.Provenance), reloaded.Events.Select(evt => evt.Provenance));
    }

    private static AnalyzerEngine CreateEngine()
    {
        var registry = new AnalyzerRegistry();
        registry.Register(new DeathEventAnalyzer());
        registry.Register(new DeathRaiseContextAnalyzer());
        registry.Register(new HealingActivityAnalyzer());
        return new AnalyzerEngine(registry);
    }

    private static IReadOnlyList<ResultProjection> ProjectResults(IReadOnlyList<AnalysisResult> results)
    {
        return results
            .Select(result => new ResultProjection(
                result.AnalyzerId,
                result.Severity,
                result.Category,
                result.Title,
                result.Summary,
                result.TimeRange,
                string.Join(",", result.Actors.Select(actor => actor.Value)),
                result.Confidence,
                string.Join(",", result.Metrics.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value:R}")),
                string.Join(";", result.Evidence.Select(evidence =>
                    $"events:{string.Join(",", evidence.EventIds.Select(id => id.Value))}|actors:{string.Join(",", evidence.ActorIds.Select(id => id.Value))}|time:{evidence.TimeRange}"))))
            .ToArray();
    }

    private static RecordedPull CreateEquivalentLocalPull()
    {
        var provenance = new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = "local:parity-fixture",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };

        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            Metadata = new PullMetadata
            {
                TerritoryId = 777,
                TerritoryName = "Parity Zone",
                StartedAt = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_001_000),
                Duration = TimeSpan.FromSeconds(20),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord { Id = Player, Name = "Player One", Kind = ActorKind.Player, JobAbbreviation = "DNC" },
                new ActorRecord { Id = Boss, Name = "Boss", Kind = ActorKind.Enemy },
            ],
            Events =
            [
                Damage(1, 5, Boss, Player, 12000, 100, provenance),
                Heal(2, 6, Player, Player, 5000, 200, provenance),
                Death(3, 12, Boss, Player, provenance),
                Raise(4, 16, Player, Player, 300, provenance),
            ],
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = "local:parity-fixture",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static FFLogsFightImportData CreateFFLogsFixture()
    {
        var fight = CreateFight();
        return new FFLogsFightImportData
        {
            ReportDocument = new FFLogsReportDocument
            {
                Report = new FFLogsReportMetadata
                {
                    Code = "PARITY123",
                    StartTimeUnixMilliseconds = 1_700_000_000_000,
                    EndTimeUnixMilliseconds = 1_700_000_100_000,
                    Revision = 1,
                },
                Fights = [fight],
            },
            Fight = fight,
            Actors =
            [
                new FFLogsReportActor { Id = 10, Name = "Player One", Type = "Player", SubType = "Dancer" },
                new FFLogsReportActor { Id = 30, Name = "Boss", Type = "Boss" },
            ],
            Events =
            [
                Event(6000, "damage", """{"sourceID":30,"targetID":10,"abilityGameID":100,"amount":12000}"""),
                Event(7000, "heal", """{"sourceID":10,"targetID":10,"abilityGameID":200,"amount":5000}"""),
                Event(13000, "death", """{"sourceID":30,"targetID":10}"""),
                Event(17000, "resurrect", """{"sourceID":10,"targetID":10,"abilityGameID":300}"""),
            ],
        };
    }

    private static FFLogsFightMetadata CreateFight()
    {
        return new FFLogsFightMetadata
        {
            Id = 42,
            EncounterId = 1234,
            Name = "Parity Encounter",
            StartTimeMilliseconds = 1000,
            EndTimeMilliseconds = 21_000,
            GameZoneId = 777,
            GameZoneName = "Parity Zone",
        };
    }

    private static FFLogsEventEnvelope Event(double timestamp, string type, string json)
    {
        using var document = JsonDocument.Parse(json);
        return new FFLogsEventEnvelope
        {
            TimestampMilliseconds = timestamp,
            Type = type,
            Payload = document.RootElement.Clone(),
        };
    }

    private static DamageEvent Damage(long sequence, double seconds, ActorId source, ActorId target, long amount, uint actionId, EventProvenance provenance)
    {
        return new DamageEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = provenance,
            Amount = amount,
            ActionId = actionId,
        };
    }

    private static HealEvent Heal(long sequence, double seconds, ActorId source, ActorId target, long amount, uint actionId, EventProvenance provenance)
    {
        return new HealEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = provenance,
            Amount = amount,
            ActionId = actionId,
        };
    }

    private static DeathEvent Death(long sequence, double seconds, ActorId source, ActorId target, EventProvenance provenance)
    {
        return new DeathEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = provenance,
        };
    }

    private static RaiseEvent Raise(long sequence, double seconds, ActorId source, ActorId target, uint actionId, EventProvenance provenance)
    {
        return new RaiseEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = provenance,
            ActionId = actionId,
        };
    }

    private sealed record ResultProjection(
        string AnalyzerId,
        AnalysisSeverity Severity,
        AnalysisCategory Category,
        string Title,
        string Summary,
        TimeRange? TimeRange,
        string Actors,
        float Confidence,
        string Metrics,
        string Evidence);
}
