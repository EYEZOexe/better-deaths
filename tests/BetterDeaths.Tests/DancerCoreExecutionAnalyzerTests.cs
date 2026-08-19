namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Jobs.Dancer;
using BetterDeaths.Domain;

public sealed class DancerCoreExecutionAnalyzerTests
{
    private static readonly ActorId Dancer = new(1);
    private static readonly ActorId PartnerA = new(2);
    private static readonly ActorId PartnerB = new(3);
    private static readonly ActorId Boss = new(4);

    [Fact]
    public async Task FullyCompletedDancesProduceNoMistakeFindings()
    {
        var pull = Pull(
            Action(1, 1.0, Dancer, 15997),
            Action(2, 2.0, Dancer, 15999),
            Action(3, 3.0, Dancer, 16000),
            Action(4, 4.0, Dancer, 16192),
            Action(5, 10.0, Dancer, 15998),
            Action(6, 11.0, Dancer, 15999),
            Action(7, 12.0, Dancer, 16000),
            Action(8, 13.0, Dancer, 16001),
            Action(9, 14.0, Dancer, 16002),
            Action(10, 15.0, Dancer, 16196));

        var run = await Analyze(pull);

        Assert.Empty(run.Failures);
        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task ExplicitUndersteppedStandardFinishProducesEvidenceBackedWarning()
    {
        var pull = Pull(
            Action(1, 1.0, Dancer, 15997),
            Action(2, 2.0, Dancer, 15999),
            Action(3, 3.0, Dancer, 16191));

        var result = Assert.Single((await Analyze(pull)).Results);

        Assert.Equal(AnalysisSeverity.Warning, result.Severity);
        Assert.Equal(AnalysisCategory.Job, result.Category);
        Assert.Contains("incomplete Standard Step", result.Title, StringComparison.Ordinal);
        Assert.Equal(2d, result.Metrics["requiredDanceSteps"]);
        Assert.Equal(1d, result.Metrics["finishVariantSteps"]);
        Assert.Equal(new[] { new EventId(1), new EventId(2), new EventId(3) }, Assert.Single(result.Evidence).EventIds);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)), result.TimeRange);
        Assert.Contains("explicit finish variant", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitUndersteppedTechnicalFinishProducesWarning()
    {
        var pull = Pull(
            Action(1, 1.0, Dancer, 15998),
            Action(2, 2.0, Dancer, 15999),
            Action(3, 3.0, Dancer, 16000),
            Action(4, 4.0, Dancer, 16194));

        var result = Assert.Single((await Analyze(pull)).Results);

        Assert.Equal(4d, result.Metrics["requiredDanceSteps"]);
        Assert.Equal(2d, result.Metrics["finishVariantSteps"]);
        Assert.Contains("Technical Step", result.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingFinishIsNotInferredAsMistake()
    {
        var pull = Pull(
            Action(1, 1.0, Dancer, 15997),
            Action(2, 2.0, Dancer, 15999),
            Action(3, 3.0, Dancer, 16000));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task ExactKnownDurationProcExpiryProducesWarning()
    {
        var pull = Pull(
            StatusApply(1, 5.0, Dancer, Dancer, 1820, durationSeconds: 30));

        var result = Assert.Single((await Analyze(pull)).Results);

        Assert.Equal(AnalysisSeverity.Warning, result.Severity);
        Assert.Contains("Threefold Fan Dance expired unused", result.Title, StringComparison.Ordinal);
        Assert.Equal(1d, result.Metrics["knownExpiry"]);
        Assert.Equal(0d, result.Metrics["consumerObserved"]);
        Assert.Equal(new[] { new EventId(1) }, Assert.Single(result.Evidence).EventIds);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(35)), result.TimeRange);
    }

    [Fact]
    public async Task ProcConsumerInsideIntervalPreventsExpiryWarning()
    {
        var pull = Pull(
            StatusApply(1, 5.0, Dancer, Dancer, 1820, durationSeconds: 30),
            Action(2, 20.0, Dancer, 16009));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task SampledProcEvidenceIsNotUsedToProveUnusedProc()
    {
        var pull = Pull(
            StatusApply(
                1,
                5.0,
                Dancer,
                Dancer,
                1820,
                durationSeconds: 30,
                fidelity: CaptureFidelity.Sampled));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task DancePartnerStatusProducesNeutralObservedAssignmentOnly()
    {
        var pull = Pull(
            StatusApply(1, 1.0, PartnerA, Dancer, 1824, durationSeconds: null));

        var result = Assert.Single((await Analyze(pull)).Results);

        Assert.Equal(AnalysisSeverity.Info, result.Severity);
        Assert.Contains("Dance Partner observed", result.Title, StringComparison.Ordinal);
        Assert.Contains(Dancer, result.Actors);
        Assert.Contains(PartnerA, result.Actors);
        Assert.Contains("does not rank partner quality", result.Summary, StringComparison.Ordinal);
        Assert.Equal(1d, result.Metrics["partnerObserved"]);
    }

    [Fact]
    public async Task KnownOverlappingPartnerIntervalsProduceContradictoryEvidenceWarning()
    {
        var pull = Pull(
            StatusApply(1, 1.0, PartnerA, Dancer, 1824, durationSeconds: 20),
            StatusApply(2, 10.0, PartnerB, Dancer, 1824, durationSeconds: 20));

        var run = await Analyze(pull);
        var warning = Assert.Single(run.Results.Where(result => result.Severity == AnalysisSeverity.Warning));

        Assert.Contains("conflicting Dance Partner evidence", warning.Title, StringComparison.Ordinal);
        Assert.Equal(11d, warning.Metrics["overlapSeconds"]);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(21)), warning.TimeRange);
        Assert.Equal(3, warning.Actors.Count);
        Assert.NotEmpty(Assert.Single(warning.Evidence).EventIds);
    }

    [Fact]
    public async Task NonDancerPullIsSkipped()
    {
        var pull = PullWithJob("BRD", Action(1, 1.0, Dancer, 15997));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
        var skipped = Assert.Single(run.Skipped);
        Assert.Equal(DancerCoreExecutionAnalyzer.AnalyzerId, skipped.AnalyzerId);
    }

    [Fact]
    public async Task EquivalentLocalAndFFLogsCanonicalFactsProduceEquivalentJobSemantics()
    {
        var events = new NormalizedEvent[]
        {
            Action(1, 1.0, Dancer, 15997),
            Action(2, 2.0, Dancer, 15999),
            Action(3, 3.0, Dancer, 16191),
            StatusApply(4, 5.0, PartnerA, Dancer, 1824, durationSeconds: null),
        };
        var local = PullWithSource(PullDataSourceKind.DalamudLive, events);
        var importedEvents = events.Select(evt => WithSource(evt, PullDataSourceKind.FFLogs)).ToArray();
        var imported = PullWithSource(PullDataSourceKind.FFLogs, importedEvents);

        var localRun = await Analyze(local);
        var importedRun = await Analyze(imported);

        Assert.Equal(Project(localRun.Results), Project(importedRun.Results));
    }

    [Fact]
    public async Task ResultIdentityIsStableAcrossRepeatedRuns()
    {
        var pull = Pull(
            Action(1, 1.0, Dancer, 15997),
            Action(2, 2.0, Dancer, 16003));

        var first = await Analyze(pull);
        var second = await Analyze(pull);

        Assert.Equal(Assert.Single(first.Results).Id, Assert.Single(second.Results).Id);
    }

    private static IReadOnlyList<ResultProjection> Project(IReadOnlyList<AnalysisResult> results)
    {
        return results.Select(result => new ResultProjection(
            result.Severity,
            result.Category,
            result.Title,
            result.Summary,
            result.TimeRange,
            string.Join(",", result.Actors.Select(actor => actor.Value)),
            result.Confidence,
            string.Join(",", result.Metrics.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value:R}")),
            string.Join(",", result.Evidence.SelectMany(evidence => evidence.EventIds).Select(id => id.Value))))
            .ToArray();
    }

    private static async Task<AnalyzerRunResult> Analyze(RecordedPull pull)
    {
        var registry = new AnalyzerRegistry();
        registry.Register(new DancerCoreExecutionAnalyzer());
        return await new AnalyzerEngine(registry).AnalyzeAsync(pull);
    }

    private static RecordedPull Pull(params NormalizedEvent[] events)
    {
        return PullWithSource(PullDataSourceKind.ImportedFile, events);
    }

    private static RecordedPull PullWithJob(string job, params NormalizedEvent[] events)
    {
        return CreatePull(PullDataSourceKind.ImportedFile, job, events);
    }

    private static RecordedPull PullWithSource(PullDataSourceKind sourceKind, params NormalizedEvent[] events)
    {
        return CreatePull(sourceKind, "DNC", events);
    }

    private static RecordedPull CreatePull(
        PullDataSourceKind sourceKind,
        string job,
        IReadOnlyList<NormalizedEvent> events)
    {
        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1,
                TerritoryName = "Dancer Test",
                Duration = TimeSpan.FromSeconds(60),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord { Id = Dancer, Name = "Dancer", Kind = ActorKind.Player, JobAbbreviation = job },
                new ActorRecord { Id = PartnerA, Name = "Partner A", Kind = ActorKind.Player, JobAbbreviation = "PCT" },
                new ActorRecord { Id = PartnerB, Name = "Partner B", Kind = ActorKind.Player, JobAbbreviation = "DRG" },
                new ActorRecord { Id = Boss, Name = "Boss", Kind = ActorKind.Enemy },
            ],
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = sourceKind,
                SourceReference = "test:dnc",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static ActionUseEvent Action(
        long sequence,
        double seconds,
        ActorId source,
        uint actionId,
        PullDataSourceKind sourceKind = PullDataSourceKind.ImportedFile)
    {
        return new ActionUseEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = Boss,
            Provenance = Provenance(sourceKind),
            ActionId = actionId,
        };
    }

    private static StatusApplyEvent StatusApply(
        long sequence,
        double seconds,
        ActorId target,
        ActorId source,
        uint statusId,
        double? durationSeconds,
        CaptureFidelity fidelity = CaptureFidelity.Exact,
        PullDataSourceKind sourceKind = PullDataSourceKind.ImportedFile)
    {
        return new StatusApplyEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = Provenance(sourceKind, fidelity),
            StatusId = statusId,
            Duration = durationSeconds is { } duration ? TimeSpan.FromSeconds(duration) : null,
        };
    }

    private static NormalizedEvent WithSource(NormalizedEvent evt, PullDataSourceKind sourceKind)
    {
        var provenance = Provenance(sourceKind, evt.Provenance.Fidelity);
        return evt switch
        {
            ActionUseEvent action => action with { Provenance = provenance },
            StatusApplyEvent status => status with { Provenance = provenance },
            _ => throw new InvalidOperationException($"Unsupported test event {evt.GetType().Name}."),
        };
    }

    private static EventProvenance Provenance(
        PullDataSourceKind sourceKind,
        CaptureFidelity fidelity = CaptureFidelity.Exact)
    {
        return new EventProvenance
        {
            SourceKind = sourceKind,
            SourceReference = "test:dnc",
            Fidelity = fidelity,
            Confidence = 1.0f,
        };
    }

    private sealed record ResultProjection(
        AnalysisSeverity Severity,
        AnalysisCategory Category,
        string Title,
        string Summary,
        TimeRange? TimeRange,
        string Actors,
        float Confidence,
        string Metrics,
        string EvidenceIds);
}
