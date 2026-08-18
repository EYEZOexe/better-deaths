namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Generic;
using BetterDeaths.Domain;

public sealed class GenericAnalyzerRegistryFixtureTests
{
    [Fact]
    public void DefaultRegistryUsesM5GenericModulesAndRetiresM3DeathSliceFromWorkspaceComposition()
    {
        var registry = GenericAnalyzerRegistryFactory.CreateDefault();
        var ids = registry.Modules.Select(module => module.Id).ToArray();

        Assert.Contains(DeathRaiseContextAnalyzer.AnalyzerId, ids);
        Assert.Contains(TargetabilityAwareUptimeAnalyzer.AnalyzerId, ids);
        Assert.Contains(HealingActivityAnalyzer.AnalyzerId, ids);
        Assert.Contains(MitigationCoverageAnalyzer.AnalyzerId, ids);
        Assert.Contains(GenericTimelineAnalyzer.AnalyzerId, ids);
        Assert.DoesNotContain(DeathEventAnalyzer.AnalyzerId, ids);
        Assert.Equal(5, ids.Length);
    }

    [Fact]
    public async Task CombinedFixtureProducesEvidenceBackedDeathRaiseHealingMitigationAndTimelineResults()
    {
        var registry = GenericAnalyzerRegistryFactory.Create(
        [
            Mitigation("test-mit", "Test Mitigation", 100, 0.20),
        ],
        [
            Cooldown("test-cd", "Test Cooldown", 900),
            Buff("test-buff", "Test Buff", 500),
        ]);
        var pull = Pull(
            StatusApply(1, 1, Player, Player, statusId: 100, durationSeconds: 20),
            StatusApply(2, 1.5, Player, Player, statusId: 500, durationSeconds: 8),
            ActionUse(3, 2, Player, Boss, actionId: 900),
            Heal(4, 3, Healer, Player, amount: 6000, actionId: 700),
            Damage(5, 9, Boss, Player, amount: 800, actionId: 1000),
            Death(6, 10, Player),
            Raise(7, 18, Healer, Player, actionId: 125));
        var engine = new AnalyzerEngine(registry);

        var first = await engine.AnalyzeAsync(pull);
        var second = await engine.AnalyzeAsync(pull);

        Assert.Empty(first.Failures);
        Assert.Contains(first.Results, result => result.AnalyzerId == DeathRaiseContextAnalyzer.AnalyzerId && result.Category == AnalysisCategory.Death);
        Assert.Contains(first.Results, result => result.AnalyzerId == DeathRaiseContextAnalyzer.AnalyzerId && result.Category == AnalysisCategory.Raise);
        Assert.Contains(first.Results, result => result.AnalyzerId == HealingActivityAnalyzer.AnalyzerId && result.Category == AnalysisCategory.Healing);
        Assert.Contains(first.Results, result => result.AnalyzerId == MitigationCoverageAnalyzer.AnalyzerId && result.Category == AnalysisCategory.Mitigation);
        Assert.Contains(first.Results, result => result.AnalyzerId == GenericTimelineAnalyzer.AnalyzerId && result.Category == AnalysisCategory.Cooldown);
        Assert.Contains(first.Results, result => result.AnalyzerId == GenericTimelineAnalyzer.AnalyzerId && result.Category == AnalysisCategory.Buff);
        Assert.DoesNotContain(first.Results, result => result.AnalyzerId == DeathEventAnalyzer.AnalyzerId);

        var death = Assert.Single(first.Results.Where(result => result.AnalyzerId == DeathRaiseContextAnalyzer.AnalyzerId && result.Category == AnalysisCategory.Death));
        Assert.Equal(0d, death.Metrics["lethalAttributionAvailable"]);
        Assert.DoesNotContain("player mistake", death.Summary, StringComparison.OrdinalIgnoreCase);

        var mitigation = Assert.Single(first.Results.Where(result => result.AnalyzerId == MitigationCoverageAnalyzer.AnalyzerId));
        Assert.Equal(0d, mitigation.Metrics["missedUseClaimed"]);
        Assert.Contains("counterfactual estimate", mitigation.Summary, StringComparison.Ordinal);

        var healing = Assert.Single(first.Results.Where(result => result.AnalyzerId == HealingActivityAnalyzer.AnalyzerId));
        Assert.Equal(AnalysisSeverity.Info, healing.Severity);
        Assert.Equal(0d, healing.Metrics["overhealKnown"]);
        Assert.Contains("not an overheal/waste judgment", healing.Summary, StringComparison.Ordinal);

        Assert.Equal(first.Results.Select(result => result.Id), second.Results.Select(result => result.Id));
        Assert.Equal(first.Results.Select(result => result.Category), second.Results.Select(result => result.Category));
    }

    [Fact]
    public async Task ForcedDowntimeFixtureDoesNotProduceGenericUptimeFinding()
    {
        var engine = new AnalyzerEngine(GenericAnalyzerRegistryFactory.CreateDefault());
        var pull = Pull(
            Targetability(1, 0, Boss, true),
            ActionUse(2, 2, Player, Boss, 100),
            Targetability(3, 4, Boss, false),
            Targetability(4, 14, Boss, true),
            ActionUse(5, 16, Player, Boss, 100));

        var run = await engine.AnalyzeAsync(pull);

        Assert.Empty(run.Failures);
        Assert.DoesNotContain(run.Results, result => result.AnalyzerId == TargetabilityAwareUptimeAnalyzer.AnalyzerId);
    }

    [Fact]
    public async Task InsufficientEvidenceFixtureDoesNotInventLethalOrMissedMitigationClaims()
    {
        var registry = GenericAnalyzerRegistryFactory.Create(
        [
            Mitigation("test-mit", "Test Mitigation", 100, 0.20),
        ],
        Array.Empty<GenericTimelineDefinition>());
        var run = await new AnalyzerEngine(registry).AnalyzeAsync(Pull(Death(1, 10, Player)));

        var death = Assert.Single(run.Results);
        Assert.Equal(DeathRaiseContextAnalyzer.AnalyzerId, death.AnalyzerId);
        Assert.Equal(0d, death.Metrics["lethalAttributionAvailable"]);
        Assert.Contains("no canonical damage event was captured", death.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("caused the death", death.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(run.Results, result => result.AnalyzerId == MitigationCoverageAnalyzer.AnalyzerId);
    }

    [Fact]
    public async Task HighRawHealingFixtureRemainsNeutralWithoutOverhealEvidence()
    {
        var engine = new AnalyzerEngine(GenericAnalyzerRegistryFactory.CreateDefault());
        var pull = Pull(
            Heal(1, 2, Healer, Player, 4_000_000_000, 700),
            Heal(2, 3, Healer, Player, 4_000_000_000, 700));

        var run = await engine.AnalyzeAsync(pull);

        var result = Assert.Single(run.Results);
        Assert.Equal(HealingActivityAnalyzer.AnalyzerId, result.AnalyzerId);
        Assert.Equal(AnalysisSeverity.Info, result.Severity);
        Assert.Equal(0d, result.Metrics["overhealKnown"]);
        Assert.Contains("not an overheal/waste judgment", result.Summary, StringComparison.Ordinal);
    }

    private static readonly ActorId Player = new(1);
    private static readonly ActorId Healer = new(2);
    private static readonly ActorId Boss = new(3);

    private static MitigationDefinition Mitigation(string id, string name, uint statusId, double reduction)
    {
        return new MitigationDefinition
        {
            Id = id,
            Name = name,
            StatusId = statusId,
            ApplicationKind = MitigationApplicationKind.TargetStatus,
            EffectKind = MitigationEffectKind.DamageReduction,
            DamageReductionFraction = reduction,
        };
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

    private static RecordedPull Pull(params NormalizedEvent[] events)
    {
        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1,
                TerritoryName = "M5 Fixture",
                Duration = TimeSpan.FromSeconds(40),
                StartedAt = new DateTimeOffset(2026, 8, 18, 18, 0, 0, TimeSpan.Zero),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord { Id = Player, Name = "Player", Kind = ActorKind.Player },
                new ActorRecord { Id = Healer, Name = "Healer", Kind = ActorKind.Player },
                new ActorRecord { Id = Boss, Name = "Boss", Kind = ActorKind.Enemy },
            ],
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.ImportedFile,
                SourceReference = "fixture:m5",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static TargetabilityEvent Targetability(long sequence, double seconds, ActorId actor, bool targetable)
    {
        return new TargetabilityEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = actor,
            TargetActorId = actor,
            Provenance = Provenance(),
            IsTargetable = targetable,
        };
    }

    private static StatusApplyEvent StatusApply(
        long sequence,
        double seconds,
        ActorId target,
        ActorId source,
        uint statusId,
        double? durationSeconds)
    {
        return new StatusApplyEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = Provenance(),
            StatusId = statusId,
            Duration = durationSeconds is { } duration ? TimeSpan.FromSeconds(duration) : null,
        };
    }

    private static ActionUseEvent ActionUse(long sequence, double seconds, ActorId source, ActorId target, uint actionId)
    {
        return new ActionUseEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = Provenance(),
            ActionId = actionId,
        };
    }

    private static HealEvent Heal(long sequence, double seconds, ActorId source, ActorId target, long amount, uint actionId)
    {
        return new HealEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = Provenance(),
            Amount = amount,
            ActionId = actionId,
        };
    }

    private static DamageEvent Damage(long sequence, double seconds, ActorId source, ActorId target, long amount, uint actionId)
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
            ActionId = actionId,
        };
    }

    private static DeathEvent Death(long sequence, double seconds, ActorId target)
    {
        return new DeathEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            TargetActorId = target,
            Provenance = Provenance(),
        };
    }

    private static RaiseEvent Raise(long sequence, double seconds, ActorId source, ActorId target, uint actionId)
    {
        return new RaiseEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = Provenance(),
            ActionId = actionId,
        };
    }

    private static EventProvenance Provenance()
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.ImportedFile,
            SourceReference = "fixture:m5",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };
    }
}
