namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Generic;
using BetterDeaths.Domain;
using System.Text.Json;

public sealed class GoldenDeathEventAnalyzerTests
{
    [Fact]
    public async Task GoldenCanonicalPullProducesStableStructuredDeathFindings()
    {
        var registry = new AnalyzerRegistry();
        registry.Register(new DeathEventAnalyzer());
        var engine = new AnalyzerEngine(registry);
        var pull = CreateGoldenPull();

        var run = await engine.AnalyzeAsync(pull);
        var repeatedRun = await engine.AnalyzeAsync(pull);

        Assert.Empty(run.Failures);
        Assert.Empty(run.Skipped);
        Assert.Equal(2, run.Results.Count);

        var first = run.Results[0];
        Assert.Equal(
            StableAnalysisResultIdentity.ForEvent(pull.Id, DeathEventAnalyzer.AnalyzerId, new EventId(3)),
            first.Id);
        Assert.Equal(DeathEventAnalyzer.AnalyzerId, first.AnalyzerId);
        Assert.Equal(AnalysisSeverity.Error, first.Severity);
        Assert.Equal(AnalysisCategory.Death, first.Category);
        Assert.Equal("Player One died", first.Title);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)), first.TimeRange);
        Assert.Equal(new[] { new ActorId(1) }, first.Actors);
        Assert.Equal(1.0f, first.Confidence);
        Assert.Equal(30d, first.Metrics["pullTimeSeconds"]);
        var firstEvidence = Assert.Single(first.Evidence);
        Assert.Equal(new[] { new EventId(3) }, firstEvidence.EventIds);
        Assert.Equal(new[] { new ActorId(1) }, firstEvidence.ActorIds);

        var second = run.Results[1];
        Assert.Equal(
            StableAnalysisResultIdentity.ForEvent(pull.Id, DeathEventAnalyzer.AnalyzerId, new EventId(5)),
            second.Id);
        Assert.Equal("Player Two died", second.Title);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(45)), second.TimeRange);
        Assert.Equal(new[] { new ActorId(2) }, second.Actors);
        Assert.Equal(0.75f, second.Confidence);
        Assert.Equal(45d, second.Metrics["pullTimeSeconds"]);
        Assert.Equal(new[] { new EventId(5) }, Assert.Single(second.Evidence).EventIds);

        var json = JsonSerializer.Serialize(run.Results);
        Assert.Equal(json, JsonSerializer.Serialize(repeatedRun.Results));

        var roundTripped = JsonSerializer.Deserialize<List<AnalysisResult>>(json);
        Assert.NotNull(roundTripped);
        Assert.Equal(run.Results.Select(result => result.Id), roundTripped.Select(result => result.Id));
        Assert.Equal(
            run.Results.Select(result => result.Evidence[0].EventIds[0]),
            roundTripped.Select(result => result.Evidence[0].EventIds[0]));
    }

    [Fact]
    public async Task PullWithoutDeathEventsSkipsGenericDeathAnalyzerWithoutFailure()
    {
        var registry = new AnalyzerRegistry();
        registry.Register(new DeathEventAnalyzer());
        var engine = new AnalyzerEngine(registry);
        var pull = CreateGoldenPull() with
        {
            Events = CreateGoldenPull().Events.Where(evt => evt is not DeathEvent).ToArray(),
        };

        // Re-sequence the non-death fixture because EventIndex intentionally rejects ambiguous/non-monotonic inputs.
        pull = pull with
        {
            Events =
            [
                Damage(1, new ActorId(3), new ActorId(1), 10),
                new TargetabilityEvent
                {
                    Id = new EventId(2),
                    Sequence = 2,
                    PullTime = TimeSpan.FromSeconds(20),
                    SourceActorId = new ActorId(3),
                    TargetActorId = new ActorId(3),
                    Provenance = Provenance(),
                    IsTargetable = false,
                },
            ],
        };

        var run = await engine.AnalyzeAsync(pull);

        Assert.Empty(run.Results);
        Assert.Empty(run.Failures);
        var skip = Assert.Single(run.Skipped);
        Assert.Equal(DeathEventAnalyzer.AnalyzerId, skip.AnalyzerId);
        Assert.Equal(AnalyzerSkipReason.Unsupported, skip.Reason);
    }

    [Fact]
    public void StableResultIdentityChangesWithPullAnalyzerOrEventIdentity()
    {
        var pullId = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        var baseline = StableAnalysisResultIdentity.ForEvent(pullId, DeathEventAnalyzer.AnalyzerId, new EventId(3));

        Assert.NotEqual(baseline, StableAnalysisResultIdentity.ForEvent(pullId, DeathEventAnalyzer.AnalyzerId, new EventId(4)));
        Assert.NotEqual(baseline, StableAnalysisResultIdentity.ForEvent(pullId, "generic.other", new EventId(3)));
        Assert.NotEqual(
            baseline,
            StableAnalysisResultIdentity.ForEvent(
                new PullId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
                DeathEventAnalyzer.AnalyzerId,
                new EventId(3)));
    }

    private static RecordedPull CreateGoldenPull()
    {
        var playerOne = new ActorId(1);
        var playerTwo = new ActorId(2);
        var boss = new ActorId(3);
        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1234,
                TerritoryName = "Golden Ultimate",
                Duration = TimeSpan.FromMinutes(10),
                StartedAt = new DateTimeOffset(2026, 8, 18, 18, 0, 0, TimeSpan.Zero),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord { Id = playerOne, Name = "Player One", Kind = ActorKind.Player },
                new ActorRecord { Id = playerTwo, Name = "Player Two", Kind = ActorKind.Player },
                new ActorRecord { Id = boss, Name = "Boss", Kind = ActorKind.Enemy },
            ],
            Events =
            [
                Damage(1, boss, playerOne, 10),
                new TargetabilityEvent
                {
                    Id = new EventId(2),
                    Sequence = 2,
                    PullTime = TimeSpan.FromSeconds(20),
                    SourceActorId = boss,
                    TargetActorId = boss,
                    Provenance = Provenance(),
                    IsTargetable = false,
                },
                new DeathEvent
                {
                    Id = new EventId(3),
                    Sequence = 3,
                    PullTime = TimeSpan.FromSeconds(30),
                    SourceActorId = boss,
                    TargetActorId = playerOne,
                    Provenance = Provenance(),
                },
                Damage(4, boss, playerTwo, 20),
                new DeathEvent
                {
                    Id = new EventId(5),
                    Sequence = 5,
                    PullTime = TimeSpan.FromSeconds(45),
                    SourceActorId = boss,
                    TargetActorId = playerTwo,
                    Provenance = Provenance(confidence: 0.75f, fidelity: CaptureFidelity.Derived),
                },
            ],
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.ImportedFile,
                SourceReference = "golden:m3-deaths",
                Fidelity = CaptureFidelity.Exact,
            },
        };
    }

    private static DamageEvent Damage(long sequence, ActorId source, ActorId target, uint actionId)
    {
        return new DamageEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(sequence * 10),
            SourceActorId = source,
            TargetActorId = target,
            Provenance = Provenance(),
            Amount = 10000,
            ActionId = actionId,
        };
    }

    private static EventProvenance Provenance(
        float confidence = 1.0f,
        CaptureFidelity fidelity = CaptureFidelity.Exact)
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.ImportedFile,
            SourceReference = "golden:m3-deaths",
            Fidelity = fidelity,
            Confidence = confidence,
        };
    }
}
