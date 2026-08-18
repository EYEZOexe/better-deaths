namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Generic;
using BetterDeaths.Domain;

public sealed class HealingActivityAnalyzerTests
{
    [Fact]
    public async Task HealingEventsProduceNeutralRawActivitySummary()
    {
        var healer = new ActorId(1);
        var target = new ActorId(2);
        var pull = Pull(
            Heal(1, 5, healer, target, amount: 12000, actionId: 100),
            Heal(2, 8, healer, target, amount: 8000, actionId: 101));

        var run = await Analyze(pull);

        Assert.Empty(run.Failures);
        var result = Assert.Single(run.Results);
        Assert.Equal(HealingActivityAnalyzer.AnalyzerId, result.AnalyzerId);
        Assert.Equal(AnalysisSeverity.Info, result.Severity);
        Assert.Equal(AnalysisCategory.Healing, result.Category);
        Assert.Equal("Healer: healing activity", result.Title);
        Assert.Equal(2d, result.Metrics["healEventCount"]);
        Assert.Equal(20000d, result.Metrics["rawHealingAmount"]);
        Assert.Equal(1d, result.Metrics["uniqueTargetCount"]);
        Assert.Equal(2d, result.Metrics["distinctActionCount"]);
        Assert.Equal(0d, result.Metrics["effectiveHealingKnown"]);
        Assert.Equal(0d, result.Metrics["overhealKnown"]);
        Assert.Equal(0d, result.Metrics["resourceCostKnown"]);
        Assert.Contains("neutral activity summary", result.Summary, StringComparison.Ordinal);
        Assert.Contains("not an overheal/waste judgment", result.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("overhealed too much", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { new EventId(1), new EventId(2) }, Assert.Single(result.Evidence).EventIds);
    }

    [Fact]
    public async Task VeryLargeRawHealingStillDoesNotBecomeWarningWithoutEffectiveHealingEvidence()
    {
        var healer = new ActorId(1);
        var target = new ActorId(2);
        var pull = Pull(
            Heal(1, 5, healer, target, amount: 4_000_000_000, actionId: 100),
            Heal(2, 6, healer, target, amount: 4_000_000_000, actionId: 100));

        var run = await Analyze(pull);

        var result = Assert.Single(run.Results);
        Assert.Equal(AnalysisSeverity.Info, result.Severity);
        Assert.Equal(8_000_000_000d, result.Metrics["rawHealingAmount"]);
        Assert.Equal(0d, result.Metrics["overhealKnown"]);
        Assert.Contains("does not encode effective healing", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealingIsGroupedBySourceActorWithSourceSpecificEvidence()
    {
        var healerA = new ActorId(1);
        var target = new ActorId(2);
        var healerB = new ActorId(3);
        var pull = PullWithActors(
            [
                Actor(healerA, "Healer A"),
                Actor(target, "Target"),
                Actor(healerB, "Healer B"),
            ],
            Heal(1, 2, healerA, target, 1000, 100),
            Heal(2, 3, healerB, target, 2000, 200),
            Heal(3, 4, healerA, target, 3000, 101));

        var run = await Analyze(pull);

        Assert.Equal(2, run.Results.Count);
        Assert.Equal("Healer A: healing activity", run.Results[0].Title);
        Assert.Equal(4000d, run.Results[0].Metrics["rawHealingAmount"]);
        Assert.Equal(new[] { new EventId(1), new EventId(3) }, Assert.Single(run.Results[0].Evidence).EventIds);
        Assert.Equal("Healer B: healing activity", run.Results[1].Title);
        Assert.Equal(2000d, run.Results[1].Metrics["rawHealingAmount"]);
        Assert.Equal(new[] { new EventId(2) }, Assert.Single(run.Results[1].Evidence).EventIds);
    }

    [Fact]
    public async Task UnknownHealingSourceRemainsNeutralAndDeterministic()
    {
        var target = new ActorId(2);
        var pull = Pull(Heal(1, 5, null, target, 5000, 100));

        var first = await Analyze(pull);
        var second = await Analyze(pull);

        var result = Assert.Single(first.Results);
        Assert.Equal("Healing activity observed", result.Title);
        Assert.Equal(new[] { target }, result.Actors);
        Assert.Equal(result.Id, Assert.Single(second.Results).Id);
    }

    [Fact]
    public async Task PullWithoutHealingSkipsAnalyzer()
    {
        var pull = Pull(new DamageEvent
        {
            Id = new EventId(1),
            Sequence = 1,
            PullTime = TimeSpan.FromSeconds(5),
            SourceActorId = new ActorId(2),
            TargetActorId = new ActorId(1),
            Provenance = Provenance(),
            Amount = 1000,
            ActionId = 100,
        });

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
        Assert.Empty(run.Failures);
        var skip = Assert.Single(run.Skipped);
        Assert.Equal(HealingActivityAnalyzer.AnalyzerId, skip.AnalyzerId);
        Assert.Equal(AnalyzerSkipReason.Unsupported, skip.Reason);
    }

    private static async Task<AnalyzerRunResult> Analyze(RecordedPull pull)
    {
        var registry = new AnalyzerRegistry();
        registry.Register(new HealingActivityAnalyzer());
        return await new AnalyzerEngine(registry).AnalyzeAsync(pull);
    }

    private static RecordedPull Pull(params NormalizedEvent[] events)
    {
        return PullWithActors(
            [
                Actor(new ActorId(1), "Healer"),
                Actor(new ActorId(2), "Target"),
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
                TerritoryName = "Healing Test",
                Duration = TimeSpan.FromSeconds(60),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors = actors,
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.ImportedFile,
                SourceReference = "test:healing",
                Fidelity = CaptureFidelity.Exact,
            },
        };
    }

    private static ActorRecord Actor(ActorId id, string name)
    {
        return new ActorRecord { Id = id, Name = name, Kind = ActorKind.Player };
    }

    private static HealEvent Heal(
        long sequence,
        double seconds,
        ActorId? source,
        ActorId target,
        uint amount,
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

    private static EventProvenance Provenance()
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.ImportedFile,
            SourceReference = "test:healing",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };
    }
}
