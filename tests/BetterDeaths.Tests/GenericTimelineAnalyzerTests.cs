namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Generic;
using BetterDeaths.Domain;

public sealed class GenericTimelineAnalyzerTests
{
    [Fact]
    public async Task ExplicitActionUsesProduceNeutralCooldownTimeline()
    {
        var pull = Pull(
            ActionUse(1, 5, Player, actionId: 100),
            Damage(2, 5, Player, Boss, actionId: 100),
            ActionUse(3, 25, Player, actionId: 100));
        var analyzer = Analyzer(Cooldown("test-cd", "Test Cooldown", 100));

        var run = await Analyze(pull, analyzer);

        var result = Assert.Single(run.Results);
        Assert.Equal(AnalysisCategory.Cooldown, result.Category);
        Assert.Equal(AnalysisSeverity.Info, result.Severity);
        Assert.Equal(2d, result.Metrics["observedUseEvidenceCount"]);
        Assert.Equal(0d, result.Metrics["expectedUsesKnown"]);
        Assert.Equal(0d, result.Metrics["availabilityKnown"]);
        Assert.Equal(0d, result.Metrics["missedUseClaimed"]);
        Assert.Equal(new[] { new EventId(1), new EventId(3) }, Assert.Single(result.Evidence).EventIds);
        Assert.Contains("does not infer expected uses", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Damage/heal packets are not reinterpreted as extra uses", Assert.Single(result.Evidence).Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CastStartIsFallbackOnlyWhenActionUseEvidenceIsAbsent()
    {
        var pull = Pull(
            CastStart(1, 5, Player, actionId: 100),
            Damage(2, 8, Player, Boss, actionId: 100));
        var analyzer = Analyzer(Cooldown("test-cd", "Test Cooldown", 100));

        var run = await Analyze(pull, analyzer);

        var result = Assert.Single(run.Results);
        Assert.Equal(1d, result.Metrics["observedUseEvidenceCount"]);
        Assert.Equal(new[] { new EventId(1) }, Assert.Single(result.Evidence).EventIds);
        Assert.Contains("CastStartEvent fallback", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DamageOnlyDoesNotGetReinterpretedAsCooldownUse()
    {
        var pull = Pull(Damage(1, 8, Player, Boss, actionId: 100));
        var analyzer = Analyzer(Cooldown("test-cd", "Test Cooldown", 100));

        var run = await Analyze(pull, analyzer);

        Assert.Empty(run.Results);
        var skip = Assert.Single(run.Skipped);
        Assert.Equal(GenericTimelineAnalyzer.AnalyzerId, skip.AnalyzerId);
    }

    [Fact]
    public async Task ConfiguredStatusTimelinePreservesKnownAndUncertainIntervals()
    {
        var pull = Pull(
            StatusApply(1, 2, Player, Player, statusId: 500, durationSeconds: 8, confidence: 0.85f),
            StatusApply(2, 20, Player, Player, statusId: 500, durationSeconds: null, confidence: 0.75f));
        var analyzer = Analyzer(Buff("test-buff", "Test Buff", 500));

        var run = await Analyze(pull, analyzer);

        var result = Assert.Single(run.Results);
        Assert.Equal(AnalysisCategory.Buff, result.Category);
        Assert.Equal(2d, result.Metrics["statusIntervalCount"]);
        Assert.Equal(1d, result.Metrics["knownEndIntervalCount"]);
        Assert.Equal(1d, result.Metrics["uncertainEndIntervalCount"]);
        Assert.Equal(0d, result.Metrics["expectedUptimeKnown"]);
        Assert.Equal(0d, result.Metrics["missedRefreshClaimed"]);
        Assert.Equal(0.75f, result.Confidence);
        Assert.Contains("not a buff-uptime optimization or missed-refresh verdict", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Unknown interval ends remain uncertain", Assert.Single(result.Evidence).Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefinitionsAreExplicitAndUnmatchedDefinitionsProduceNoPseudoFindings()
    {
        var pull = Pull(ActionUse(1, 5, Player, actionId: 999));
        var analyzer = Analyzer(
            Cooldown("test-cd", "Test Cooldown", 100),
            Buff("test-buff", "Test Buff", 500));

        var run = await Analyze(pull, analyzer);

        Assert.Empty(run.Results);
        Assert.Single(run.Skipped);
    }

    [Fact]
    public void DuplicateReferenceOrDefinitionIdIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => Analyzer(
            Cooldown("same", "A", 100),
            Cooldown("same", "B", 200)));

        Assert.Throws<InvalidOperationException>(() => Analyzer(
            Cooldown("a", "A", 100),
            Cooldown("b", "B", 100)));
    }

    [Fact]
    public async Task TimelineResultIdentityIsStableAcrossRuns()
    {
        var pull = Pull(
            ActionUse(1, 5, Player, 100),
            ActionUse(2, 20, Player, 100));
        var analyzer = Analyzer(Cooldown("test-cd", "Test Cooldown", 100));

        var first = await Analyze(pull, analyzer);
        var second = await Analyze(pull, analyzer);

        Assert.Equal(Assert.Single(first.Results).Id, Assert.Single(second.Results).Id);
    }

    private static readonly ActorId Player = new(1);
    private static readonly ActorId Boss = new(2);

    private static GenericTimelineAnalyzer Analyzer(params GenericTimelineDefinition[] definitions)
    {
        return new GenericTimelineAnalyzer(definitions);
    }

    private static GenericTimelineDefinition Cooldown(string id, string name, uint actionId)
    {
        return new GenericTimelineDefinition
        {
            Id = id,
            Name = name,
            Kind = GenericTimelineKind.CooldownAction,
            ReferenceId = actionId,
        };
    }

    private static GenericTimelineDefinition Buff(string id, string name, uint statusId)
    {
        return new GenericTimelineDefinition
        {
            Id = id,
            Name = name,
            Kind = GenericTimelineKind.BuffStatus,
            ReferenceId = statusId,
        };
    }

    private static async Task<AnalyzerRunResult> Analyze(RecordedPull pull, IAnalyzerModule analyzer)
    {
        var registry = new AnalyzerRegistry();
        registry.Register(analyzer);
        return await new AnalyzerEngine(registry).AnalyzeAsync(pull);
    }

    private static RecordedPull Pull(params NormalizedEvent[] events)
    {
        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1,
                TerritoryName = "Timeline Test",
                Duration = TimeSpan.FromSeconds(40),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord { Id = Player, Name = "Player", Kind = ActorKind.Player },
                new ActorRecord { Id = Boss, Name = "Boss", Kind = ActorKind.Enemy },
            ],
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.ImportedFile,
                SourceReference = "test:timeline",
                Fidelity = CaptureFidelity.Exact,
            },
        };
    }

    private static ActionUseEvent ActionUse(long sequence, double seconds, ActorId source, uint actionId)
    {
        return new ActionUseEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = Boss,
            Provenance = Provenance(),
            ActionId = actionId,
        };
    }

    private static CastStartEvent CastStart(long sequence, double seconds, ActorId source, uint actionId)
    {
        return new CastStartEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = Boss,
            Provenance = Provenance(),
            ActionId = actionId,
            CastDuration = TimeSpan.FromSeconds(2),
        };
    }

    private static DamageEvent Damage(long sequence, double seconds, ActorId source, ActorId target, uint actionId)
    {
        return new DamageEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = Provenance(),
            ActionId = actionId,
            Amount = 1000,
        };
    }

    private static StatusApplyEvent StatusApply(
        long sequence,
        double seconds,
        ActorId actor,
        ActorId source,
        uint statusId,
        double? durationSeconds,
        float confidence)
    {
        return new StatusApplyEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = actor,
            Provenance = Provenance(confidence),
            StatusId = statusId,
            Duration = durationSeconds is { } duration ? TimeSpan.FromSeconds(duration) : null,
        };
    }

    private static EventProvenance Provenance(float confidence = 1.0f)
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.ImportedFile,
            SourceReference = "test:timeline",
            Fidelity = CaptureFidelity.Exact,
            Confidence = confidence,
        };
    }
}
