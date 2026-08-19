namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Jobs.Dancer;
using BetterDeaths.Domain;

public sealed class M7DancerIntegrationTests
{
    private static readonly ActorId Dancer = new(1);
    private static readonly ActorId Partner = new(2);
    private static readonly ActorId Boss = new(3);

    [Fact]
    public async Task CombinedDancerFixtureRunsCoreBurstCooldownUptimeAndPartnerAnalysisTogether()
    {
        var pull = ProblemFixture(PullDataSourceKind.ImportedFile);

        var run = await Analyze(pull);

        Assert.Empty(run.Failures);
        Assert.Empty(run.Skipped);
        Assert.Contains(run.Results, result => result.AnalyzerId == DancerCoreExecutionAnalyzer.AnalyzerId);
        Assert.Contains(run.Results, result => result.AnalyzerId == DancerBurstAndUptimeAnalyzer.AnalyzerId);
        Assert.Contains(run.Results, result => result.Title.Contains("incomplete Standard Step", StringComparison.Ordinal));
        Assert.Contains(run.Results, result => result.Title.Contains("Threefold Fan Dance expired unused", StringComparison.Ordinal));
        Assert.Contains(run.Results, result => result.Title.Contains("Dance Partner observed", StringComparison.Ordinal));
        Assert.Contains(run.Results, result => result.Title.Contains("Devilment delayed", StringComparison.Ordinal));
        Assert.Contains(run.Results, result => result.Title.Contains("Flourish drift", StringComparison.Ordinal));
        Assert.Contains(run.Results, result => result.Title.Contains("targetable GCD gap", StringComparison.Ordinal));
        Assert.All(run.Results, result => Assert.Equal(AnalysisCategory.Job, result.Category));

        var warnings = run.Results
            .Where(result => result.Severity is AnalysisSeverity.Warning or AnalysisSeverity.Error)
            .ToArray();
        Assert.NotEmpty(warnings);
        Assert.All(warnings, result =>
        {
            Assert.NotEmpty(result.Actors);
            Assert.NotNull(result.TimeRange);
            Assert.InRange(result.Confidence, 0.0f, 1.0f);
            Assert.NotEmpty(result.Evidence);
            Assert.All(result.Evidence, evidence =>
            {
                Assert.NotEmpty(evidence.EventIds);
                Assert.NotEmpty(evidence.ActorIds);
                Assert.NotNull(evidence.TimeRange);
            });
        });

        Assert.DoesNotContain(run.Results, result =>
            result.Title.Contains("Esprit", StringComparison.OrdinalIgnoreCase) ||
            result.Title.Contains("feather", StringComparison.OrdinalIgnoreCase) ||
            result.Summary.Contains("overcap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CleanEvidenceSupportedDancerFixtureProducesNoInventedJobFindings()
    {
        var pull = Pull(
            PullDataSourceKind.ImportedFile,
            durationSeconds: 20,
            Action(1, 1.0, 15997),
            Action(2, 2.0, 15999),
            Action(3, 3.0, 16000),
            Action(4, 4.0, 16192),
            Action(5, 10.0, 15998),
            Action(6, 11.0, 15999),
            Action(7, 12.0, 16000),
            Action(8, 13.0, 16001),
            Action(9, 14.0, 16002),
            Action(10, 15.0, 16196),
            Action(11, 15.1, 16011));

        var run = await Analyze(pull);

        Assert.Empty(run.Failures);
        Assert.Empty(run.Skipped);
        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task EquivalentLocalAndFFLogsCanonicalPullsProduceEquivalentCombinedDancerSemantics()
    {
        var local = ProblemFixture(PullDataSourceKind.DalamudLive);
        var imported = ProblemFixture(PullDataSourceKind.FFLogs);

        var localRun = await Analyze(local);
        var importedRun = await Analyze(imported);

        Assert.Empty(localRun.Failures);
        Assert.Empty(importedRun.Failures);
        Assert.Equal(Project(localRun.Results), Project(importedRun.Results));
    }

    [Fact]
    public async Task GaugeEventWithoutVerifiedDancerGaugeContractDoesNotCreateResourceFinding()
    {
        var pull = Pull(
            PullDataSourceKind.ImportedFile,
            durationSeconds: 20,
            Gauge(1, 5.0, "dnc.esprit", 100),
            Gauge(2, 6.0, "dnc.feathers", 4));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
    }

    private static RecordedPull ProblemFixture(PullDataSourceKind sourceKind)
    {
        return Pull(
            sourceKind,
            durationSeconds: 90,
            Targetable(1, 0.0, true, sourceKind),
            Status(2, 1.0, Partner, Dancer, 1824, null, sourceKind),
            Action(3, 5.0, 15997, sourceKind),
            Action(4, 6.0, 15999, sourceKind),
            Action(5, 7.0, 16191, sourceKind),
            Action(6, 10.0, 16013, sourceKind),
            Action(7, 20.0, 16196, sourceKind),
            Action(8, 21.0, 16005, sourceKind),
            Action(9, 22.0, 16011, sourceKind),
            Status(10, 30.0, Dancer, Dancer, 1820, 30, sourceKind),
            Gauge(11, 40.0, "dnc.esprit", 100, sourceKind),
            Action(12, 77.0, 16013, sourceKind));
    }

    private static async Task<AnalyzerRunResult> Analyze(RecordedPull pull)
    {
        var registry = new AnalyzerRegistry();
        registry.Register(new DancerCoreExecutionAnalyzer());
        registry.Register(new DancerBurstAndUptimeAnalyzer());
        return await new AnalyzerEngine(registry).AnalyzeAsync(pull);
    }

    private static IReadOnlyList<ResultProjection> Project(IReadOnlyList<AnalysisResult> results)
    {
        return results.Select(result => new ResultProjection(
            result.AnalyzerId,
            result.Severity,
            result.Category,
            result.Title,
            result.Summary,
            result.TimeRange,
            string.Join(",", result.Actors.Select(actor => actor.Value)),
            result.Confidence,
            string.Join(",", result.Metrics.OrderBy(metric => metric.Key).Select(metric => $"{metric.Key}={metric.Value:R}")),
            string.Join(";", result.Evidence.Select(evidence =>
                $"events:{string.Join(",", evidence.EventIds.Select(id => id.Value))}|actors:{string.Join(",", evidence.ActorIds.Select(id => id.Value))}|time:{evidence.TimeRange}"))))
            .ToArray();
    }

    private static RecordedPull Pull(
        PullDataSourceKind sourceKind,
        double durationSeconds,
        params NormalizedEvent[] events)
    {
        var normalizedEvents = events.Select(evt => WithSource(evt, sourceKind)).ToArray();
        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("77777777-8888-9999-aaaa-bbbbbbbbbbbb")),
            Metadata = new PullMetadata
            {
                TerritoryId = 777,
                TerritoryName = "M7 Dancer Integration",
                Duration = TimeSpan.FromSeconds(durationSeconds),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord { Id = Dancer, Name = "Dancer", Kind = ActorKind.Player, JobAbbreviation = "DNC" },
                new ActorRecord { Id = Partner, Name = "Partner", Kind = ActorKind.Player, JobAbbreviation = "PCT" },
                new ActorRecord { Id = Boss, Name = "Boss", Kind = ActorKind.Enemy },
            ],
            Events = normalizedEvents,
            Provenance = new PullProvenance
            {
                SourceKind = sourceKind,
                SourceReference = "m7:dnc:integration",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static ActionUseEvent Action(
        long sequence,
        double seconds,
        uint actionId,
        PullDataSourceKind sourceKind = PullDataSourceKind.ImportedFile)
    {
        return new ActionUseEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = Dancer,
            TargetActorId = Boss,
            Provenance = Provenance(sourceKind),
            ActionId = actionId,
        };
    }

    private static StatusApplyEvent Status(
        long sequence,
        double seconds,
        ActorId target,
        ActorId source,
        uint statusId,
        double? durationSeconds,
        PullDataSourceKind sourceKind)
    {
        return new StatusApplyEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = Provenance(sourceKind),
            StatusId = statusId,
            Duration = durationSeconds is { } duration ? TimeSpan.FromSeconds(duration) : null,
        };
    }

    private static TargetabilityEvent Targetable(
        long sequence,
        double seconds,
        bool targetable,
        PullDataSourceKind sourceKind)
    {
        return new TargetabilityEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = Boss,
            TargetActorId = Boss,
            Provenance = Provenance(sourceKind),
            IsTargetable = targetable,
        };
    }

    private static GaugeEvent Gauge(
        long sequence,
        double seconds,
        string key,
        double value,
        PullDataSourceKind sourceKind = PullDataSourceKind.ImportedFile)
    {
        return new GaugeEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = Dancer,
            TargetActorId = Dancer,
            Provenance = Provenance(sourceKind),
            GaugeKey = key,
            Value = value,
        };
    }

    private static NormalizedEvent WithSource(NormalizedEvent evt, PullDataSourceKind sourceKind)
    {
        var provenance = Provenance(sourceKind, evt.Provenance.Fidelity);
        return evt switch
        {
            ActionUseEvent action => action with { Provenance = provenance },
            StatusApplyEvent status => status with { Provenance = provenance },
            TargetabilityEvent targetability => targetability with { Provenance = provenance },
            GaugeEvent gauge => gauge with { Provenance = provenance },
            _ => throw new InvalidOperationException($"Unsupported M7 integration event {evt.GetType().Name}."),
        };
    }

    private static EventProvenance Provenance(
        PullDataSourceKind sourceKind,
        CaptureFidelity fidelity = CaptureFidelity.Exact)
    {
        return new EventProvenance
        {
            SourceKind = sourceKind,
            SourceReference = "m7:dnc:integration",
            Fidelity = fidelity,
            Confidence = 1.0f,
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
