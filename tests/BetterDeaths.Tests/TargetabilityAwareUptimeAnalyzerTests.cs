namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Generic;
using BetterDeaths.Domain;

public sealed class TargetabilityAwareUptimeAnalyzerTests
{
    [Fact]
    public async Task LongGapDuringKnownTargetableWindowProducesObservation()
    {
        var pull = Pull(
            Targetability(1, 0, true),
            PlayerAction(2, 2),
            PlayerAction(3, 12));

        var run = await Analyze(pull);

        var result = Assert.Single(run.Results);
        Assert.Equal(TargetabilityAwareUptimeAnalyzer.AnalyzerId, result.AnalyzerId);
        Assert.Equal(AnalysisSeverity.Observation, result.Severity);
        Assert.Equal(AnalysisCategory.Uptime, result.Category);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(12)), result.TimeRange);
        Assert.Equal(10d, result.Metrics["observedGapSeconds"]);
        Assert.Contains("not a job-rotation or blame verdict", result.Summary, StringComparison.Ordinal);
        Assert.Equal(new[] { new ActorId(1), new ActorId(2) }, result.Actors);
        var evidence = Assert.Single(result.Evidence);
        Assert.Contains(new EventId(1), evidence.EventIds);
        Assert.Contains(new EventId(2), evidence.EventIds);
        Assert.Contains(new EventId(3), evidence.EventIds);
    }

    [Fact]
    public async Task ForcedUntargetableDowntimeIsNotReportedAsPlayerInactivity()
    {
        var pull = Pull(
            Targetability(1, 0, true),
            PlayerAction(2, 2),
            Targetability(3, 4, false),
            Targetability(4, 14, true),
            PlayerAction(5, 16));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
        Assert.Empty(run.Failures);
    }

    [Fact]
    public async Task SamePlayerTimingWithoutForcedDowntimeDoesProduceObservation()
    {
        var pull = Pull(
            Targetability(1, 0, true),
            PlayerAction(2, 2),
            PlayerAction(3, 16));

        var run = await Analyze(pull);

        var result = Assert.Single(run.Results);
        Assert.Equal(14d, result.Metrics["observedGapSeconds"]);
    }

    [Fact]
    public async Task UnknownTargetabilityBeforeFirstObservationIsNotTreatedAsActiveTime()
    {
        var pull = Pull(
            PlayerAction(1, 2),
            Targetability(2, 10, true),
            PlayerAction(3, 13));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task DeathDowntimeIsDeferredToDeathRaiseAnalysis()
    {
        var pull = Pull(
            Targetability(1, 0, true),
            PlayerAction(2, 2),
            Death(3, 6),
            PlayerAction(4, 15));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task StableActorWindowIdentityIsDeterministicAcrossRuns()
    {
        var pull = Pull(
            Targetability(1, 0, true),
            PlayerAction(2, 2),
            PlayerAction(3, 12));

        var first = await Analyze(pull);
        var second = await Analyze(pull);

        Assert.Equal(Assert.Single(first.Results).Id, Assert.Single(second.Results).Id);
    }

    [Fact]
    public async Task PrimaryEnemySelectionPrefersObservedPlayerInteractions()
    {
        var player = new ActorId(1);
        var boss = new ActorId(2);
        var add = new ActorId(3);
        var pull = PullWithActors(
            [
                Actor(player, "Player", ActorKind.Player),
                Actor(boss, "Boss", ActorKind.Enemy),
                Actor(add, "Add", ActorKind.Enemy),
            ],
            Targetability(1, 0, true, boss),
            Targetability(2, 0, true, add),
            Damage(3, 1, player, boss),
            Damage(4, 2, player, boss),
            PlayerAction(5, 3, player, boss),
            PlayerAction(6, 12, player, boss));

        var run = await Analyze(pull);

        var result = Assert.Single(run.Results);
        Assert.Equal((double)boss.Value, result.Metrics["targetActorId"]);
    }

    private static async Task<AnalyzerRunResult> Analyze(RecordedPull pull)
    {
        var registry = new AnalyzerRegistry();
        registry.Register(new TargetabilityAwareUptimeAnalyzer());
        return await new AnalyzerEngine(registry).AnalyzeAsync(pull);
    }

    private static RecordedPull Pull(params NormalizedEvent[] events)
    {
        return PullWithActors(
            [
                Actor(new ActorId(1), "Player", ActorKind.Player),
                Actor(new ActorId(2), "Boss", ActorKind.Enemy),
            ],
            events);
    }

    private static RecordedPull PullWithActors(
        IReadOnlyList<ActorRecord> actors,
        params NormalizedEvent[] events)
    {
        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1,
                TerritoryName = "Uptime Test",
                Duration = TimeSpan.FromSeconds(20),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors = actors,
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.ImportedFile,
                SourceReference = "test:uptime",
                Fidelity = CaptureFidelity.Exact,
            },
        };
    }

    private static ActorRecord Actor(ActorId id, string name, ActorKind kind)
    {
        return new ActorRecord { Id = id, Name = name, Kind = kind };
    }

    private static TargetabilityEvent Targetability(
        long sequence,
        double seconds,
        bool targetable,
        ActorId? actor = null)
    {
        var resolved = actor ?? new ActorId(2);
        return new TargetabilityEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = resolved,
            TargetActorId = resolved,
            Provenance = Provenance(),
            IsTargetable = targetable,
        };
    }

    private static ActionUseEvent PlayerAction(
        long sequence,
        double seconds,
        ActorId? source = null,
        ActorId? target = null)
    {
        return new ActionUseEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source ?? new ActorId(1),
            TargetActorId = target ?? new ActorId(2),
            Provenance = Provenance(),
            ActionId = 100,
        };
    }

    private static DamageEvent Damage(
        long sequence,
        double seconds,
        ActorId source,
        ActorId target)
    {
        return new DamageEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = Provenance(),
            ActionId = 101,
            Amount = 1000,
        };
    }

    private static DeathEvent Death(long sequence, double seconds)
    {
        return new DeathEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            TargetActorId = new ActorId(1),
            Provenance = Provenance(),
        };
    }

    private static EventProvenance Provenance()
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.ImportedFile,
            SourceReference = "test:uptime",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };
    }
}
