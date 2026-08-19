namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Generic;
using BetterDeaths.Domain;

public sealed class MitigationCoverageAnalyzerTests
{
    [Fact]
    public async Task KnownTargetMitigationProducesCoverageAndExplicitCounterfactualEstimate()
    {
        var pull = Pull(
            StatusApply(1, 0, actor: Player, source: Player, statusId: 100, durationSeconds: 20, confidence: 0.85f),
            Damage(2, 10, source: Boss, target: Player, amount: 800));
        var analyzer = Analyzer(
            Definition("personal-20", "Personal 20%", 100, MitigationApplicationKind.TargetStatus, 0.20));

        var run = await Analyze(pull, analyzer);

        Assert.Empty(run.Failures);
        var result = Assert.Single(run.Results);
        Assert.Equal(AnalysisCategory.Mitigation, result.Category);
        Assert.Equal(AnalysisSeverity.Info, result.Severity);
        Assert.Equal(1d, result.Metrics["activeMitigationCount"]);
        Assert.Equal(1d, result.Metrics["activePersonalMitigationCount"]);
        Assert.Equal(0d, result.Metrics["activePartyWideMitigationCount"]);
        Assert.Equal(0.20d, result.Metrics["configuredCombinedReductionFraction"], 6);
        Assert.Equal(1000d, result.Metrics["estimatedWithoutModeledReduction"], 6);
        Assert.Equal(200d, result.Metrics["estimatedModeledReductionAmount"], 6);
        Assert.Equal(0d, result.Metrics["availabilityKnown"]);
        Assert.Equal(0d, result.Metrics["missedUseClaimed"]);
        Assert.Equal(0.85f, result.Confidence);
        Assert.Contains("(personal)", result.Summary, StringComparison.Ordinal);
        Assert.Contains("explicit assumption", result.Summary, StringComparison.Ordinal);
        Assert.Contains("counterfactual estimate", result.Summary, StringComparison.Ordinal);
        Assert.Contains("not reconstructed server damage or a survival claim", result.Summary, StringComparison.Ordinal);
        var evidence = Assert.Single(result.Evidence);
        Assert.Equal(new[] { new EventId(2), new EventId(1) }, evidence.EventIds);
    }

    [Fact]
    public async Task DamageSourceDebuffUsesStatusOnDamageSource()
    {
        var pull = Pull(
            StatusApply(1, 0, actor: Boss, source: Player, statusId: 200, durationSeconds: 20),
            Damage(2, 10, source: Boss, target: Player, amount: 900));
        var analyzer = Analyzer(
            Definition("source-10", "Source debuff 10%", 200, MitigationApplicationKind.DamageSourceStatus, 0.10));

        var run = await Analyze(pull, analyzer);

        var result = Assert.Single(run.Results);
        Assert.Equal(0.10d, result.Metrics["configuredCombinedReductionFraction"], 6);
        Assert.Equal(1d, result.Metrics["activeDamageSourceDebuffCount"]);
        Assert.Contains("(damage-source debuff)", result.Summary, StringComparison.Ordinal);
        Assert.Contains(new ActorId(2), result.Actors);
    }

    [Fact]
    public async Task TargetStatusScopesRemainDistinctInStructuredResults()
    {
        var pull = Pull(
            StatusApply(1, 0, Player, Player, 100, 20),
            StatusApply(2, 0, Player, Player, 101, 20),
            Damage(3, 10, Boss, Player, 640));
        var analyzer = Analyzer(
            Definition(
                "personal-20",
                "Personal 20%",
                100,
                MitigationApplicationKind.TargetStatus,
                0.20,
                MitigationScopeKind.Personal),
            Definition(
                "party-20",
                "Party 20%",
                101,
                MitigationApplicationKind.TargetStatus,
                0.20,
                MitigationScopeKind.PartyWide));

        var run = await Analyze(pull, analyzer);

        var result = Assert.Single(run.Results);
        Assert.Equal(1d, result.Metrics["activePersonalMitigationCount"]);
        Assert.Equal(1d, result.Metrics["activePartyWideMitigationCount"]);
        Assert.Equal(0d, result.Metrics["activeTargetedMitigationCount"]);
        Assert.Contains("Personal 20% (personal)", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Party 20% (party-wide)", result.Summary, StringComparison.Ordinal);
        Assert.Contains("does not collapse personal, targeted, and party-wide semantics", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OverlapIsNeutralObservationAndNeverAutomaticallyWaste()
    {
        var pull = Pull(
            StatusApply(1, 0, Player, Player, 100, 20),
            StatusApply(2, 0, Boss, Player, 200, 20),
            Damage(3, 10, Boss, Player, 720));
        var analyzer = Analyzer(
            Definition("personal-20", "Personal 20%", 100, MitigationApplicationKind.TargetStatus, 0.20),
            Definition("source-10", "Source debuff 10%", 200, MitigationApplicationKind.DamageSourceStatus, 0.10));

        var run = await Analyze(pull, analyzer);

        var result = Assert.Single(run.Results);
        Assert.Equal(AnalysisSeverity.Observation, result.Severity);
        Assert.Equal(2d, result.Metrics["activeMitigationCount"]);
        Assert.Equal(1d, result.Metrics["overlapObserved"]);
        Assert.Equal(0.28d, result.Metrics["configuredCombinedReductionFraction"], 6);
        Assert.Equal(1000d, result.Metrics["estimatedWithoutModeledReduction"], 6);
        Assert.Contains("Overlap is coverage evidence, not automatically waste", result.Summary, StringComparison.Ordinal);
        Assert.Equal(0d, result.Metrics["missedUseClaimed"]);
    }

    [Fact]
    public async Task AbsentMitigationDoesNotBecomeMissedUseFinding()
    {
        var pull = Pull(Damage(1, 10, Boss, Player, 1000));
        var analyzer = Analyzer(
            Definition("personal-20", "Personal 20%", 100, MitigationApplicationKind.TargetStatus, 0.20));

        var run = await Analyze(pull, analyzer);

        Assert.Empty(run.Results);
        Assert.Empty(run.Failures);
    }

    [Fact]
    public async Task UnknownStatusEndIsNotAssumedToCoverDamage()
    {
        var pull = Pull(
            StatusApply(1, 0, Player, Player, 100, durationSeconds: null),
            Damage(2, 10, Boss, Player, 1000));
        var analyzer = Analyzer(
            Definition("personal-20", "Personal 20%", 100, MitigationApplicationKind.TargetStatus, 0.20));

        var run = await Analyze(pull, analyzer);

        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task ShieldCoverageCanBeObservedWithoutInventingReductionEstimate()
    {
        var pull = Pull(
            StatusApply(1, 0, Player, Player, 300, 20),
            Damage(2, 10, Boss, Player, 1000));
        var analyzer = Analyzer(new MitigationDefinition
        {
            Id = "shield-status",
            Name = "Shield",
            StatusId = 300,
            ApplicationKind = MitigationApplicationKind.TargetStatus,
            ScopeKind = MitigationScopeKind.Targeted,
            EffectKind = MitigationEffectKind.Shield,
        });

        var run = await Analyze(pull, analyzer);

        var result = Assert.Single(run.Results);
        Assert.Equal(1d, result.Metrics["activeTargetedMitigationCount"]);
        Assert.Equal(0d, result.Metrics["whatIfEstimateAvailable"]);
        Assert.False(result.Metrics.ContainsKey("configuredCombinedReductionFraction"));
        Assert.Contains("(targeted)", result.Summary, StringComparison.Ordinal);
        Assert.Contains("does not claim", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateConfiguredStatusApplicationIsRejected()
    {
        var first = Definition("a", "A", 100, MitigationApplicationKind.TargetStatus, 0.10);
        var second = Definition("b", "B", 100, MitigationApplicationKind.TargetStatus, 0.20, MitigationScopeKind.PartyWide);

        var error = Assert.Throws<InvalidOperationException>(() => Analyzer(first, second));

        Assert.Contains("configured more than once", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IncompatibleDamageSourceScopeIsRejected()
    {
        var definition = Definition(
            "invalid-scope",
            "Invalid Scope",
            100,
            MitigationApplicationKind.DamageSourceStatus,
            0.10,
            MitigationScopeKind.PartyWide);

        var error = Assert.Throws<InvalidOperationException>(() => Analyzer(definition));

        Assert.Contains("incompatible scope", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidReductionDefinitionIsRejected()
    {
        var definition = Definition("invalid", "Invalid", 100, MitigationApplicationKind.TargetStatus, 1.0);

        Assert.Throws<InvalidOperationException>(() => Analyzer(definition));
    }

    [Fact]
    public async Task SameConfiguredMitigationProducesStableResultIdentity()
    {
        var pull = Pull(
            StatusApply(1, 0, Player, Player, 100, 20),
            Damage(2, 10, Boss, Player, 800));
        var analyzer = Analyzer(
            Definition("personal-20", "Personal 20%", 100, MitigationApplicationKind.TargetStatus, 0.20));

        var first = await Analyze(pull, analyzer);
        var second = await Analyze(pull, analyzer);

        Assert.Equal(Assert.Single(first.Results).Id, Assert.Single(second.Results).Id);
    }

    private static readonly ActorId Player = new(1);
    private static readonly ActorId Boss = new(2);

    private static MitigationCoverageAnalyzer Analyzer(params MitigationDefinition[] definitions)
    {
        return new MitigationCoverageAnalyzer(definitions);
    }

    private static MitigationDefinition Definition(
        string id,
        string name,
        uint statusId,
        MitigationApplicationKind application,
        double reduction,
        MitigationScopeKind? scope = null)
    {
        return new MitigationDefinition
        {
            Id = id,
            Name = name,
            StatusId = statusId,
            ApplicationKind = application,
            ScopeKind = scope ?? (application == MitigationApplicationKind.DamageSourceStatus
                ? MitigationScopeKind.DamageSourceDebuff
                : MitigationScopeKind.Personal),
            EffectKind = MitigationEffectKind.DamageReduction,
            DamageReductionFraction = reduction,
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
                TerritoryName = "Mitigation Test",
                Duration = TimeSpan.FromSeconds(30),
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
                SourceReference = "test:mitigation",
                Fidelity = CaptureFidelity.Exact,
            },
        };
    }

    private static StatusApplyEvent StatusApply(
        long sequence,
        double seconds,
        ActorId actor,
        ActorId source,
        uint statusId,
        double? durationSeconds,
        float confidence = 1.0f)
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

    private static DamageEvent Damage(
        long sequence,
        double seconds,
        ActorId source,
        ActorId target,
        long amount)
    {
        return new DamageEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = Provenance(),
            Amount = amount,
            ActionId = 1000,
        };
    }

    private static EventProvenance Provenance(float confidence = 1.0f)
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.ImportedFile,
            SourceReference = "test:mitigation",
            Fidelity = CaptureFidelity.Exact,
            Confidence = confidence,
        };
    }
}
