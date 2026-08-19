namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Generic;
using BetterDeaths.Domain;

public sealed class M5GenericAnalysisIntegrationTests
{
    private static readonly ActorId PlayerA = new(1);
    private static readonly ActorId PlayerB = new(2);
    private static readonly ActorId Boss = new(3);

    [Fact]
    public async Task RepresentativeProgressionPullProducesEvidenceBackedGenericResultsWithoutNaiveBlame()
    {
        var engine = CreateEngine();
        var pull = CreateRepresentativePull();

        var first = await engine.AnalyzeAsync(pull);
        var second = await engine.AnalyzeAsync(pull);

        Assert.Empty(first.Failures);
        Assert.Equal(first.Results.Select(result => result.Id), second.Results.Select(result => result.Id));

        var death = Assert.Single(first.Results, result =>
            result.AnalyzerId == DeathRaiseContextAnalyzer.AnalyzerId &&
            result.Category == AnalysisCategory.Death);
        Assert.Contains(new EventId(11), death.Evidence.SelectMany(evidence => evidence.EventIds));
        Assert.Contains("does not label any nearby hit as lethal or as a player mistake", death.Summary, StringComparison.Ordinal);

        var raise = Assert.Single(first.Results, result =>
            result.AnalyzerId == DeathRaiseContextAnalyzer.AnalyzerId &&
            result.Category == AnalysisCategory.Raise);
        Assert.Contains(new EventId(12), raise.Evidence.SelectMany(evidence => evidence.EventIds));

        var healing = Assert.Single(first.Results, result => result.AnalyzerId == HealingActivityAnalyzer.AnalyzerId);
        Assert.Equal(AnalysisSeverity.Info, healing.Severity);
        Assert.Equal(0d, healing.Metrics["overhealKnown"]);
        Assert.Equal(0d, healing.Metrics["resourceCostKnown"]);
        Assert.Contains("neutral activity summary—not an overheal/waste judgment", healing.Summary, StringComparison.Ordinal);

        var mitigatedDamage = first.Results
            .Where(result => result.AnalyzerId == MitigationCoverageAnalyzer.AnalyzerId)
            .First(result => result.TimeRange?.Start == TimeSpan.FromSeconds(10));
        Assert.Equal(AnalysisSeverity.Observation, mitigatedDamage.Severity);
        Assert.Equal(2d, mitigatedDamage.Metrics["activeMitigationCount"]);
        Assert.Equal(1d, mitigatedDamage.Metrics["activePersonalMitigationCount"]);
        Assert.Equal(1d, mitigatedDamage.Metrics["activeDamageSourceDebuffCount"]);
        Assert.Equal(0d, mitigatedDamage.Metrics["missedUseClaimed"]);
        Assert.Contains("not automatically waste", mitigatedDamage.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Unknown open-ended mitigation", mitigatedDamage.Summary, StringComparison.Ordinal);

        var uptimeResults = first.Results
            .Where(result => result.AnalyzerId == TargetabilityAwareUptimeAnalyzer.AnalyzerId)
            .ToArray();
        Assert.NotEmpty(uptimeResults);
        Assert.All(uptimeResults, result =>
        {
            Assert.NotNull(result.TimeRange);
            var range = result.TimeRange!.Value;
            Assert.True(
                range.End <= TimeSpan.FromSeconds(35) || range.Start >= TimeSpan.FromSeconds(45),
                $"Uptime finding {range.Start}-{range.End} crossed the evidence-supported forced untargetable window.");
        });

        var cooldown = Assert.Single(first.Results, result =>
            result.AnalyzerId == GenericTimelineAnalyzer.AnalyzerId &&
            result.Category == AnalysisCategory.Cooldown);
        Assert.Equal(2d, cooldown.Metrics["observedUseEvidenceCount"]);
        Assert.Equal(0d, cooldown.Metrics["expectedUsesKnown"]);
        Assert.Equal(0d, cooldown.Metrics["missedUseClaimed"]);

        var buff = Assert.Single(first.Results, result =>
            result.AnalyzerId == GenericTimelineAnalyzer.AnalyzerId &&
            result.Category == AnalysisCategory.Buff);
        Assert.Equal(1d, buff.Metrics["knownEndIntervalCount"]);
        Assert.Equal(0d, buff.Metrics["missedRefreshClaimed"]);
    }

    [Fact]
    public async Task CleanPullWithoutSupportedEvidenceDoesNotInventGenericFindings()
    {
        var engine = CreateEngine();
        var pull = CreateRepresentativePull() with { Events = Array.Empty<NormalizedEvent>() };

        var run = await engine.AnalyzeAsync(pull);

        Assert.Empty(run.Results);
        Assert.Empty(run.Failures);
        Assert.Equal(5, run.Skipped.Count);
    }

    private static AnalyzerEngine CreateEngine()
    {
        var registry = new AnalyzerRegistry();
        registry.Register(new DeathRaiseContextAnalyzer());
        registry.Register(new HealingActivityAnalyzer());
        registry.Register(new TargetabilityAwareUptimeAnalyzer());
        registry.Register(new MitigationCoverageAnalyzer(
        [
            new MitigationDefinition
            {
                Id = "personal-20",
                Name = "Personal 20%",
                StatusId = 100,
                ApplicationKind = MitigationApplicationKind.TargetStatus,
                ScopeKind = MitigationScopeKind.Personal,
                EffectKind = MitigationEffectKind.DamageReduction,
                DamageReductionFraction = 0.20,
            },
            new MitigationDefinition
            {
                Id = "source-10",
                Name = "Source debuff 10%",
                StatusId = 200,
                ApplicationKind = MitigationApplicationKind.DamageSourceStatus,
                ScopeKind = MitigationScopeKind.DamageSourceDebuff,
                EffectKind = MitigationEffectKind.DamageReduction,
                DamageReductionFraction = 0.10,
            },
            new MitigationDefinition
            {
                Id = "unknown-open",
                Name = "Unknown open-ended mitigation",
                StatusId = 201,
                ApplicationKind = MitigationApplicationKind.TargetStatus,
                ScopeKind = MitigationScopeKind.Targeted,
                EffectKind = MitigationEffectKind.DamageReduction,
                DamageReductionFraction = 0.10,
            },
        ]));
        registry.Register(new GenericTimelineAnalyzer(
        [
            new GenericTimelineDefinition
            {
                Id = "configured-cooldown",
                Name = "Configured Cooldown",
                Kind = GenericTimelineKind.CooldownAction,
                ReferenceId = 900,
            },
            new GenericTimelineDefinition
            {
                Id = "configured-buff",
                Name = "Configured Buff",
                Kind = GenericTimelineKind.BuffStatus,
                ReferenceId = 500,
            },
        ]));
        return new AnalyzerEngine(registry);
    }

    private static RecordedPull CreateRepresentativePull()
    {
        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("55555555-6666-7777-8888-999999999999")),
            Metadata = new PullMetadata
            {
                TerritoryId = 999,
                TerritoryName = "M5 Integration Fixture",
                Duration = TimeSpan.FromSeconds(60),
                StartedAt = new DateTimeOffset(2026, 8, 19, 4, 0, 0, TimeSpan.Zero),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord { Id = PlayerA, Name = "Player A", Kind = ActorKind.Player },
                new ActorRecord { Id = PlayerB, Name = "Player B", Kind = ActorKind.Player },
                new ActorRecord { Id = Boss, Name = "Boss", Kind = ActorKind.Enemy },
            ],
            Events =
            [
                Targetability(1, 0, true),
                StatusApply(2, 1, Boss, PlayerB, 200, 30),
                StatusApply(3, 2, PlayerA, PlayerA, 100, 30),
                StatusApply(4, 2.5, PlayerA, PlayerB, 201, null),
                StatusApply(5, 3, PlayerB, PlayerB, 500, 10),
                ActionUse(6, 4, PlayerB, Boss, 900),
                Damage(7, 5, PlayerB, Boss, 100, 901),
                Heal(8, 6, PlayerA, PlayerA, 250000, 700),
                Damage(9, 10, Boss, PlayerA, 720, 1000),
                Damage(10, 12, PlayerB, Boss, 100, 902),
                Death(11, 20, Boss, PlayerA),
                Raise(12, 25, PlayerB, PlayerA, 173),
                ActionUse(13, 30, PlayerB, Boss, 900),
                Damage(14, 30.1, PlayerB, Boss, 100, 903),
                Targetability(15, 35, false),
                Targetability(16, 45, true),
                Damage(17, 50, PlayerB, Boss, 100, 904),
            ],
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.ImportedFile,
                SourceReference = "fixture:m5-integration",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static TargetabilityEvent Targetability(long sequence, double seconds, bool targetable)
    {
        return new TargetabilityEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = Boss,
            TargetActorId = Boss,
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

    private static ActionUseEvent ActionUse(
        long sequence,
        double seconds,
        ActorId source,
        ActorId target,
        uint actionId)
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

    private static DamageEvent Damage(
        long sequence,
        double seconds,
        ActorId source,
        ActorId target,
        long amount,
        uint actionId)
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

    private static HealEvent Heal(
        long sequence,
        double seconds,
        ActorId source,
        ActorId target,
        long amount,
        uint actionId)
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

    private static DeathEvent Death(long sequence, double seconds, ActorId source, ActorId target)
    {
        return new DeathEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = Provenance(),
        };
    }

    private static RaiseEvent Raise(
        long sequence,
        double seconds,
        ActorId source,
        ActorId target,
        uint actionId)
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
            SourceReference = "fixture:m5-integration",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };
    }
}
