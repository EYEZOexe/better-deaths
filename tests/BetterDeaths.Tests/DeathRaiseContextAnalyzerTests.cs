namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Generic;
using BetterDeaths.Domain;

public sealed class DeathRaiseContextAnalyzerTests
{
    [Fact]
    public async Task RecentDamageIsContextButNeverPromotedToLethalOrPlayerMistake()
    {
        var pull = Pull(
            Damage(1, 22, amount: 1000),
            Damage(2, 28, amount: 999999),
            Death(3, 30));

        var run = await Analyze(pull);

        Assert.Empty(run.Failures);
        var result = Assert.Single(run.Results);
        Assert.Equal(DeathRaiseContextAnalyzer.AnalyzerId, result.AnalyzerId);
        Assert.Equal(AnalysisCategory.Death, result.Category);
        Assert.Equal(AnalysisSeverity.Error, result.Severity);
        Assert.Equal(2d, result.Metrics["recentDamageEvents"]);
        Assert.Equal(0d, result.Metrics["lethalAttributionAvailable"]);
        Assert.Contains("does not label any nearby hit as lethal or as a player mistake", result.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("caused the death", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("last hit", result.Summary, StringComparison.OrdinalIgnoreCase);

        var damageEvidence = Assert.Single(result.Evidence.Where(evidence => evidence.EventIds.Contains(new EventId(2))));
        Assert.Contains("fatal-context candidates only", damageEvidence.Explanation, StringComparison.Ordinal);
        Assert.Contains(new EventId(1), damageEvidence.EventIds);
        Assert.Contains(new EventId(2), damageEvidence.EventIds);
    }

    [Fact]
    public async Task NoRecentDamageProducesExplicitInsufficientLethalAttribution()
    {
        var pull = Pull(Death(1, 30));

        var run = await Analyze(pull);

        var result = Assert.Single(run.Results);
        Assert.Equal(0d, result.Metrics["recentDamageEvents"]);
        Assert.Equal(0d, result.Metrics["lethalAttributionAvailable"]);
        Assert.Contains("no canonical damage event was captured", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not label any nearby hit as lethal", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KnownStatusAtDeathIsRecordedAsContextNotCause()
    {
        var pull = Pull(
            StatusApply(1, 20, statusId: 500, durationSeconds: 20),
            Death(2, 30));

        var run = await Analyze(pull);

        var result = Assert.Single(run.Results);
        Assert.Equal(1d, result.Metrics["knownStatusIntervalsAtDeath"]);
        Assert.Equal(0d, result.Metrics["uncertainStatusIntervalsAtDeath"]);
        var statusEvidence = Assert.Single(result.Evidence.Where(evidence => evidence.EventIds.Contains(new EventId(1))));
        Assert.Contains("context, not proof that a status caused the death", statusEvidence.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownStatusEndRemainsUncertainRatherThanAssumedActive()
    {
        var pull = Pull(
            StatusApply(1, 20, statusId: 500, durationSeconds: null),
            Death(2, 30));

        var run = await Analyze(pull);

        var result = Assert.Single(run.Results);
        Assert.Equal(0d, result.Metrics["knownStatusIntervalsAtDeath"]);
        Assert.Equal(1d, result.Metrics["uncertainStatusIntervalsAtDeath"]);
        var statusEvidence = Assert.Single(result.Evidence.Where(evidence => evidence.EventIds.Contains(new EventId(1))));
        Assert.Contains("not assumed active", statusEvidence.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RaiseAfterDeathProducesSeparateDownstreamObservationWithoutClaimingRecoveryCompletion()
    {
        var raiser = new ActorId(3);
        var pull = PullWithActors(
            DefaultActors().Append(new ActorRecord { Id = raiser, Name = "Healer", Kind = ActorKind.Player }).ToArray(),
            Death(1, 30),
            Raise(2, 38, raiser, actionId: 125));

        var run = await Analyze(pull);

        Assert.Equal(2, run.Results.Count);
        var death = Assert.Single(run.Results.Where(result => result.Category == AnalysisCategory.Death));
        var raise = Assert.Single(run.Results.Where(result => result.Category == AnalysisCategory.Raise));
        Assert.Equal(1d, death.Metrics["raiseObserved"]);
        Assert.Equal(8d, death.Metrics["secondsToRaiseObservation"]);
        Assert.Equal(8d, raise.Metrics["secondsAfterDeath"]);
        Assert.Equal(125d, raise.Metrics["raiseActionId"]);
        Assert.Equal(new[] { new ActorId(1), raiser }, raise.Actors);
        Assert.Contains("not confirmed resurrection completion", raise.Summary, StringComparison.Ordinal);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(38)), raise.TimeRange);
        Assert.Equal(new[] { new EventId(1), new EventId(2) }, Assert.Single(raise.Evidence).EventIds);
    }

    [Fact]
    public async Task RaiseAfterASecondDeathIsNotAttachedToTheEarlierDeath()
    {
        var raiser = new ActorId(3);
        var pull = PullWithActors(
            DefaultActors().Append(new ActorRecord { Id = raiser, Name = "Healer", Kind = ActorKind.Player }).ToArray(),
            Death(1, 10),
            Death(2, 20),
            Raise(3, 25, raiser, actionId: 125));

        var run = await Analyze(pull);

        var deathResults = run.Results.Where(result => result.Category == AnalysisCategory.Death).ToArray();
        Assert.Equal(2, deathResults.Length);
        Assert.Equal(0d, deathResults[0].Metrics["raiseObserved"]);
        Assert.Equal(1d, deathResults[1].Metrics["raiseObserved"]);
        Assert.Single(run.Results.Where(result => result.Category == AnalysisCategory.Raise));
    }

    [Fact]
    public async Task RaiseBeyondBoundedObservationWindowIsNotLinked()
    {
        var raiser = new ActorId(3);
        var pull = PullWithActors(
            DefaultActors().Append(new ActorRecord { Id = raiser, Name = "Healer", Kind = ActorKind.Player }).ToArray(),
            new NormalizedEvent[]
            {
                Death(1, 10),
                Raise(2, 75, raiser, actionId: 125),
            },
            durationSeconds: 90);

        var run = await Analyze(pull);

        var death = Assert.Single(run.Results);
        Assert.Equal(AnalysisCategory.Death, death.Category);
        Assert.Equal(0d, death.Metrics["raiseObserved"]);
    }

    [Fact]
    public async Task CleanPullWithoutDeathsSkipsAnalyzer()
    {
        var pull = Pull(Damage(1, 10, 1000));

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
        Assert.Empty(run.Failures);
        var skip = Assert.Single(run.Skipped);
        Assert.Equal(DeathRaiseContextAnalyzer.AnalyzerId, skip.AnalyzerId);
        Assert.Equal(AnalyzerSkipReason.Unsupported, skip.Reason);
    }

    [Fact]
    public async Task ResultIdentitiesRemainStableAcrossRepeatedRuns()
    {
        var raiser = new ActorId(3);
        var pull = PullWithActors(
            DefaultActors().Append(new ActorRecord { Id = raiser, Name = "Healer", Kind = ActorKind.Player }).ToArray(),
            Damage(1, 28, 5000),
            Death(2, 30),
            Raise(3, 38, raiser, 125));

        var first = await Analyze(pull);
        var second = await Analyze(pull);

        Assert.Equal(first.Results.Select(result => result.Id), second.Results.Select(result => result.Id));
        Assert.Equal(first.Results.Select(result => result.Category), second.Results.Select(result => result.Category));
    }

    private static async Task<AnalyzerRunResult> Analyze(RecordedPull pull)
    {
        var registry = new AnalyzerRegistry();
        registry.Register(new DeathRaiseContextAnalyzer());
        return await new AnalyzerEngine(registry).AnalyzeAsync(pull);
    }

    private static RecordedPull Pull(params NormalizedEvent[] events)
    {
        return PullWithActors(DefaultActors(), events);
    }

    private static RecordedPull PullWithActors(
        IReadOnlyList<ActorRecord> actors,
        params NormalizedEvent[] events)
    {
        return PullWithActors(actors, events, durationSeconds: 80);
    }

    private static RecordedPull PullWithActors(
        IReadOnlyList<ActorRecord> actors,
        NormalizedEvent[] events,
        double durationSeconds)
    {
        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1,
                TerritoryName = "Death Context Test",
                Duration = TimeSpan.FromSeconds(durationSeconds),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors = actors,
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.ImportedFile,
                SourceReference = "test:death-context",
                Fidelity = CaptureFidelity.Exact,
            },
        };
    }

    private static IReadOnlyList<ActorRecord> DefaultActors()
    {
        return
        [
            new ActorRecord { Id = new ActorId(1), Name = "Player", Kind = ActorKind.Player },
            new ActorRecord { Id = new ActorId(2), Name = "Boss", Kind = ActorKind.Enemy },
        ];
    }

    private static DamageEvent Damage(long sequence, double seconds, uint amount)
    {
        return new DamageEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = new ActorId(2),
            TargetActorId = new ActorId(1),
            Provenance = Provenance(),
            Amount = amount,
            ActionId = 100,
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

    private static RaiseEvent Raise(
        long sequence,
        double seconds,
        ActorId source,
        uint actionId)
    {
        return new RaiseEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = new ActorId(1),
            Provenance = Provenance(confidence: 0.9f),
            ActionId = actionId,
        };
    }

    private static StatusApplyEvent StatusApply(
        long sequence,
        double seconds,
        uint statusId,
        double? durationSeconds)
    {
        return new StatusApplyEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = new ActorId(2),
            TargetActorId = new ActorId(1),
            Provenance = Provenance(),
            StatusId = statusId,
            Duration = durationSeconds is { } duration ? TimeSpan.FromSeconds(duration) : null,
        };
    }

    private static EventProvenance Provenance(float confidence = 1.0f)
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.ImportedFile,
            SourceReference = "test:death-context",
            Fidelity = CaptureFidelity.Exact,
            Confidence = confidence,
        };
    }
}
