namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Jobs.Dancer;
using BetterDeaths.Domain;
using System.Runtime.CompilerServices;

public sealed class DancerBurstAndUptimeAnalyzerTests
{
    private static readonly ActorId Dancer = new(1);
    private static readonly ActorId Boss = new(2);

    [Fact]
    public async Task ImmediateDevilmentAfterTechnicalFinishIsClean()
    {
        var pull = Pull(
            durationSeconds: 30,
            Action(1, 5.0, 16196),
            Action(2, 5.5, 16011));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task InterveningActionBeforeDevilmentProducesAlignmentOptimization()
    {
        var pull = Pull(
            durationSeconds: 30,
            Action(1, 5.0, 16196),
            Action(2, 6.0, 16005),
            Action(3, 7.0, 16011));

        var result = Assert.Single((await Analyze(pull)).Results);

        Assert.Equal(AnalysisSeverity.Optimization, result.Severity);
        Assert.Equal(AnalysisCategory.Job, result.Category);
        Assert.Contains("Devilment delayed", result.Title, StringComparison.Ordinal);
        Assert.Equal(1d, result.Metrics["interveningActionCount"]);
        Assert.Equal(new[] { new EventId(1), new EventId(2), new EventId(3) }, Assert.Single(result.Evidence).EventIds);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(7)), result.TimeRange);
    }

    [Fact]
    public async Task MissingPrecedingTechnicalFinishDoesNotBecomeAlignmentMistake()
    {
        var pull = Pull(durationSeconds: 30, Action(1, 25.0, 16011));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task DevilmentAfterObservedTechnicalWindowProducesWarning()
    {
        var pull = Pull(
            durationSeconds: 30,
            Action(1, 1.0, 16196),
            Action(2, 25.0, 16011));

        var result = Assert.Single((await Analyze(pull)).Results);

        Assert.Equal(AnalysisSeverity.Warning, result.Severity);
        Assert.Contains("after the observed Technical Finish window", result.Title, StringComparison.Ordinal);
        Assert.Equal(1d, result.Metrics["technicalFinishObserved"]);
        Assert.Equal(24d, result.Metrics["devilmentDelaySeconds"]);
        Assert.Equal(new[] { new EventId(1), new EventId(2) }, Assert.Single(result.Evidence).EventIds);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(25)), result.TimeRange);
    }

    [Fact]
    public async Task NonExactBoundaryDoesNotProveOutsideTechnicalAlignment()
    {
        var pull = Pull(
            durationSeconds: 30,
            Action(1, 1.0, 16196),
            Action(2, 25.0, 16011, fidelity: CaptureFidelity.Sampled));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task FlourishDriftUsesKnownTargetableTimeOnly()
    {
        var pull = Pull(
            durationSeconds: 80,
            Targetable(1, 0.0, true),
            Action(2, 5.0, 16013),
            Action(3, 72.0, 16013));

        var result = Assert.Single((await Analyze(pull)).Results);

        Assert.Equal(AnalysisSeverity.Optimization, result.Severity);
        Assert.Contains("Flourish drift", result.Title, StringComparison.Ordinal);
        Assert.Equal(67d, result.Metrics["knownTargetableSeconds"]);
        Assert.Equal(7d, result.Metrics["activeDriftSeconds"]);
        Assert.Equal(0d, result.Metrics["terminalOpportunity"]);
    }

    [Fact]
    public async Task ForcedUntargetableTimeDoesNotBecomeCooldownDrift()
    {
        var pull = Pull(
            durationSeconds: 80,
            Targetable(1, 0.0, true),
            Action(2, 5.0, 16013),
            Targetable(3, 30.0, false),
            Targetable(4, 60.0, true),
            Action(5, 72.0, 16013));

        var run = await Analyze(pull);

        Assert.DoesNotContain(run.Results, result => result.Title.Contains("Flourish drift", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExactLastUseWithFullTargetableCooldownOpportunityProducesWarning()
    {
        var pull = Pull(
            durationSeconds: 75,
            Targetable(1, 0.0, true),
            Action(2, 5.0, 16013));

        var result = Assert.Single((await Analyze(pull)).Results);

        Assert.Equal(AnalysisSeverity.Warning, result.Severity);
        Assert.Contains("additional Flourish opportunity", result.Title, StringComparison.Ordinal);
        Assert.Equal(70d, result.Metrics["knownTargetableSeconds"]);
        Assert.Equal(10d, result.Metrics["activeDriftSeconds"]);
        Assert.Equal(1d, result.Metrics["terminalOpportunity"]);
    }

    [Fact]
    public async Task SampledLastUseDoesNotProveMissedTerminalCooldownUse()
    {
        var pull = Pull(
            durationSeconds: 75,
            Targetable(1, 0.0, true),
            Action(2, 5.0, 16013, fidelity: CaptureFidelity.Sampled));

        var run = await Analyze(pull);

        Assert.DoesNotContain(run.Results, result => result.Title.Contains("additional Flourish opportunity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TargetableDancerGcdGapProducesOptimization()
    {
        var pull = Pull(
            durationSeconds: 20,
            Targetable(1, 0.0, true),
            Action(2, 5.0, 15989),
            Action(3, 12.0, 15990));

        var result = Assert.Single((await Analyze(pull)).Results);

        Assert.Equal(AnalysisSeverity.Optimization, result.Severity);
        Assert.Contains("targetable GCD gap", result.Title, StringComparison.Ordinal);
        Assert.Equal(7d, result.Metrics["targetableGcdGapSeconds"]);
        Assert.Contains(new EventId(1), Assert.Single(result.Evidence).EventIds);
        Assert.Contains(new EventId(2), Assert.Single(result.Evidence).EventIds);
        Assert.Contains(new EventId(3), Assert.Single(result.Evidence).EventIds);
    }

    [Fact]
    public async Task ForcedDowntimeInsideGcdPairDoesNotProduceGapFinding()
    {
        var pull = Pull(
            durationSeconds: 20,
            Targetable(1, 0.0, true),
            Action(2, 5.0, 15989),
            Targetable(3, 6.0, false),
            Targetable(4, 11.0, true),
            Action(5, 12.0, 15990));

        var run = await Analyze(pull);

        Assert.DoesNotContain(run.Results, result => result.Title.Contains("GCD gap", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeathContainingGcdGapIsDeferredInsteadOfChargedAsExecutionLoss()
    {
        var pull = Pull(
            durationSeconds: 20,
            Targetable(1, 0.0, true),
            Action(2, 5.0, 15989),
            Death(3, 8.0),
            Action(4, 15.0, 15990));

        var run = await Analyze(pull);

        Assert.DoesNotContain(run.Results, result => result.Title.Contains("GCD gap", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoCooldownUseDoesNotInventFirstUseReadiness()
    {
        var pull = Pull(
            durationSeconds: 180,
            Targetable(1, 0.0, true),
            Action(2, 5.0, 15989),
            Action(3, 7.0, 15990));

        var run = await Analyze(pull);

        Assert.DoesNotContain(run.Results, result =>
            result.Title.Contains("Technical Step opportunity", StringComparison.Ordinal) ||
            result.Title.Contains("Devilment opportunity", StringComparison.Ordinal) ||
            result.Title.Contains("Flourish opportunity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EquivalentLocalAndFFLogsCanonicalFactsProduceEquivalentSemantics()
    {
        var local = PullWithSource(
            PullDataSourceKind.DalamudLive,
            durationSeconds: 80,
            Targetable(1, 0.0, true, PullDataSourceKind.DalamudLive),
            Action(2, 5.0, 16013, sourceKind: PullDataSourceKind.DalamudLive),
            Action(3, 72.0, 16013, sourceKind: PullDataSourceKind.DalamudLive));
        var imported = PullWithSource(
            PullDataSourceKind.FFLogs,
            durationSeconds: 80,
            Targetable(1, 0.0, true, PullDataSourceKind.FFLogs),
            Action(2, 5.0, 16013, sourceKind: PullDataSourceKind.FFLogs),
            Action(3, 72.0, 16013, sourceKind: PullDataSourceKind.FFLogs));

        var localRun = await Analyze(local);
        var importedRun = await Analyze(imported);

        Assert.Equal(Project(localRun.Results), Project(importedRun.Results));
    }

    [Fact]
    public async Task GaugePresenceAloneDoesNotFabricateResourceVerdict()
    {
        var pull = Pull(
            durationSeconds: 20,
            Gauge(1, 5.0, "dnc.esprit", 100));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task NonDancerPullIsSkipped()
    {
        var pull = PullWithJob(
            "BRD",
            durationSeconds: 20,
            Action(1, 5.0, 16013));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
        Assert.Equal(DancerBurstAndUptimeAnalyzer.AnalyzerId, Assert.Single(run.Skipped).AnalyzerId);
    }

    [Fact]
    public void AnalyzerBoundaryContainsNoSourceUiNetworkOrPersistenceDependency()
    {
        var source = ReadRepositoryFile("BetterDeaths/Analysis/Jobs/Dancer/DancerBurstAndUptimeAnalyzer.cs");

        Assert.DoesNotContain("FFLogs", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dalamud", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ImGui", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IPullStore", source, StringComparison.Ordinal);
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
        registry.Register(new DancerBurstAndUptimeAnalyzer());
        return await new AnalyzerEngine(registry).AnalyzeAsync(pull);
    }

    private static RecordedPull Pull(double durationSeconds, params NormalizedEvent[] events)
    {
        return CreatePull(PullDataSourceKind.ImportedFile, "DNC", durationSeconds, events);
    }

    private static RecordedPull PullWithSource(
        PullDataSourceKind sourceKind,
        double durationSeconds,
        params NormalizedEvent[] events)
    {
        return CreatePull(sourceKind, "DNC", durationSeconds, events);
    }

    private static RecordedPull PullWithJob(string job, double durationSeconds, params NormalizedEvent[] events)
    {
        return CreatePull(PullDataSourceKind.ImportedFile, job, durationSeconds, events);
    }

    private static RecordedPull CreatePull(
        PullDataSourceKind sourceKind,
        string job,
        double durationSeconds,
        IReadOnlyList<NormalizedEvent> events)
    {
        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("22222222-3333-4444-5555-666666666666")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1,
                TerritoryName = "Dancer Burst Test",
                Duration = TimeSpan.FromSeconds(durationSeconds),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord { Id = Dancer, Name = "Dancer", Kind = ActorKind.Player, JobAbbreviation = job },
                new ActorRecord { Id = Boss, Name = "Boss", Kind = ActorKind.Enemy },
            ],
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = sourceKind,
                SourceReference = "test:dnc-burst",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static ActionUseEvent Action(
        long sequence,
        double seconds,
        uint actionId,
        CaptureFidelity fidelity = CaptureFidelity.Exact,
        PullDataSourceKind sourceKind = PullDataSourceKind.ImportedFile)
    {
        return new ActionUseEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = Dancer,
            TargetActorId = Boss,
            Provenance = Provenance(sourceKind, fidelity),
            ActionId = actionId,
        };
    }

    private static TargetabilityEvent Targetable(
        long sequence,
        double seconds,
        bool isTargetable,
        PullDataSourceKind sourceKind = PullDataSourceKind.ImportedFile)
    {
        return new TargetabilityEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = Boss,
            TargetActorId = Boss,
            Provenance = Provenance(sourceKind),
            IsTargetable = isTargetable,
        };
    }

    private static DeathEvent Death(long sequence, double seconds)
    {
        return new DeathEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = Boss,
            TargetActorId = Dancer,
            Provenance = Provenance(PullDataSourceKind.ImportedFile),
        };
    }

    private static GaugeEvent Gauge(long sequence, double seconds, string key, double value)
    {
        return new GaugeEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = Dancer,
            TargetActorId = Dancer,
            Provenance = Provenance(PullDataSourceKind.ImportedFile),
            GaugeKey = key,
            Value = value,
        };
    }

    private static EventProvenance Provenance(
        PullDataSourceKind sourceKind,
        CaptureFidelity fidelity = CaptureFidelity.Exact)
    {
        return new EventProvenance
        {
            SourceKind = sourceKind,
            SourceReference = "test:dnc-burst",
            Fidelity = fidelity,
            Confidence = 1.0f,
        };
    }

    private static string ReadRepositoryFile(
        string relativePath,
        [CallerFilePath] string testSourcePath = "")
    {
        var testDirectory = Path.GetDirectoryName(testSourcePath)
            ?? throw new InvalidOperationException("Could not resolve test source directory.");
        var repositoryRoot = Path.GetFullPath(Path.Combine(testDirectory, "..", ".."));
        return File.ReadAllText(Path.Combine(
            repositoryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
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
