namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Generic;
using BetterDeaths.Domain;

public sealed class GenericTimelinePerActorFallbackTests
{
    [Fact]
    public async Task OneActorsActionUseDoesNotHideAnotherActorsCastStartFallback()
    {
        var playerA = new ActorId(1);
        var playerB = new ActorId(2);
        var boss = new ActorId(3);
        var pull = new RecordedPull
        {
            Id = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1,
                TerritoryName = "Timeline Fallback Test",
                Duration = TimeSpan.FromSeconds(30),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord { Id = playerA, Name = "Player A", Kind = ActorKind.Player },
                new ActorRecord { Id = playerB, Name = "Player B", Kind = ActorKind.Player },
                new ActorRecord { Id = boss, Name = "Boss", Kind = ActorKind.Enemy },
            ],
            Events =
            [
                new ActionUseEvent
                {
                    Id = new EventId(1),
                    Sequence = 1,
                    PullTime = TimeSpan.FromSeconds(5),
                    SourceActorId = playerA,
                    TargetActorId = boss,
                    Provenance = Provenance(),
                    ActionId = 100,
                },
                new CastStartEvent
                {
                    Id = new EventId(2),
                    Sequence = 2,
                    PullTime = TimeSpan.FromSeconds(6),
                    SourceActorId = playerB,
                    TargetActorId = boss,
                    Provenance = Provenance(),
                    ActionId = 100,
                    CastDuration = TimeSpan.FromSeconds(2),
                },
            ],
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.ImportedFile,
                SourceReference = "test:timeline-fallback",
                Fidelity = CaptureFidelity.Exact,
            },
        };
        var registry = new AnalyzerRegistry();
        registry.Register(new GenericTimelineAnalyzer(
        [
            new GenericTimelineDefinition
            {
                Id = "test-cd",
                Name = "Test Cooldown",
                Kind = GenericTimelineKind.CooldownAction,
                ReferenceId = 100,
            },
        ]));

        var run = await new AnalyzerEngine(registry).AnalyzeAsync(pull);

        Assert.Empty(run.Failures);
        Assert.Equal(2, run.Results.Count);
        Assert.Equal("Player A: Test Cooldown timeline", run.Results[0].Title);
        Assert.Equal(new[] { new EventId(1) }, Assert.Single(run.Results[0].Evidence).EventIds);
        Assert.Contains("ActionUseEvent", run.Results[0].Summary, StringComparison.Ordinal);
        Assert.Equal("Player B: Test Cooldown timeline", run.Results[1].Title);
        Assert.Equal(new[] { new EventId(2) }, Assert.Single(run.Results[1].Evidence).EventIds);
        Assert.Contains("CastStartEvent fallback", run.Results[1].Summary, StringComparison.Ordinal);
    }

    private static EventProvenance Provenance()
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.ImportedFile,
            SourceReference = "test:timeline-fallback",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };
    }
}
