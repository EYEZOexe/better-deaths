namespace BetterDeaths;

using BetterDeaths.Analysis.Encounters.DancingMadUltimate;
using BetterDeaths.Analysis.Generic;
using BetterDeaths.Analysis.Jobs.Dancer;
using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using BetterDeaths.Windows.Analyzer;

public sealed class AnalyzerWorkspaceDataControllerTests
{
    [Fact]
    public async Task QueryPullsUsesPullStoreBoundaryAndRequestedLimit()
    {
        var pull = CreatePull(includeDeath: true);
        var store = new FakePullStore(pull);
        var controller = AnalyzerWorkspaceDataController.CreateDefault(store);

        var summaries = await controller.QueryPullsAsync(25);

        var summary = Assert.Single(summaries);
        Assert.Equal(pull.Id, summary.Id);
        Assert.Equal(25, store.LastQueryLimit);
    }

    [Fact]
    public async Task LoadPullRunsDefaultM5AnalyzersAndPrecomputesDeathEventsOnce()
    {
        var pull = CreatePull(includeDeath: true);
        var store = new FakePullStore(pull);
        var controller = AnalyzerWorkspaceDataController.CreateDefault(store);

        var loaded = await controller.LoadPullAsync(pull.Id);

        Assert.NotNull(loaded);
        Assert.Same(pull, loaded.Pull);
        var death = Assert.Single(loaded.DeathEvents);
        Assert.Equal(new EventId(2), death.Id);

        var result = Assert.Single(loaded.Results);
        Assert.Equal(DeathRaiseContextAnalyzer.AnalyzerId, result.AnalyzerId);
        Assert.Equal(AnalysisCategory.Death, result.Category);
        Assert.Contains(death.Id, result.Evidence.SelectMany(evidence => evidence.EventIds));
        Assert.Empty(loaded.Failures);
        Assert.Contains(loaded.Skipped, skip => skip.AnalyzerId == HealingActivityAnalyzer.AnalyzerId);
        Assert.Contains(loaded.Skipped, skip => skip.AnalyzerId == TargetabilityAwareUptimeAnalyzer.AnalyzerId);
        Assert.Contains(loaded.Skipped, skip => skip.AnalyzerId == DancerCoreExecutionAnalyzer.AnalyzerId);
        Assert.Contains(loaded.Skipped, skip => skip.AnalyzerId == DancerBurstAndUptimeAnalyzer.AnalyzerId);
        Assert.Contains(loaded.Skipped, skip => skip.AnalyzerId == ForsakenOpeningAssignmentAnalyzer.AnalyzerId);
    }

    [Fact]
    public async Task LoadPullWithoutDeathReturnsSuccessfulPullWithUnsupportedDefaultAnalyzers()
    {
        var pull = CreatePull(includeDeath: false);
        var store = new FakePullStore(pull);
        var controller = AnalyzerWorkspaceDataController.CreateDefault(store);

        var loaded = await controller.LoadPullAsync(pull.Id);

        Assert.NotNull(loaded);
        Assert.Empty(loaded.DeathEvents);
        Assert.Empty(loaded.Results);
        Assert.Empty(loaded.Failures);
        Assert.Equal(6, loaded.Skipped.Count);
        Assert.Contains(loaded.Skipped, skip => skip.AnalyzerId == DeathRaiseContextAnalyzer.AnalyzerId);
        Assert.Contains(loaded.Skipped, skip => skip.AnalyzerId == HealingActivityAnalyzer.AnalyzerId);
        Assert.Contains(loaded.Skipped, skip => skip.AnalyzerId == TargetabilityAwareUptimeAnalyzer.AnalyzerId);
        Assert.Contains(loaded.Skipped, skip => skip.AnalyzerId == DancerCoreExecutionAnalyzer.AnalyzerId);
        Assert.Contains(loaded.Skipped, skip => skip.AnalyzerId == DancerBurstAndUptimeAnalyzer.AnalyzerId);
        Assert.Contains(loaded.Skipped, skip => skip.AnalyzerId == ForsakenOpeningAssignmentAnalyzer.AnalyzerId);
    }

    [Fact]
    public async Task DefaultWorkspaceRunsDancerJobAnalyzersThroughSameEngineComposition()
    {
        var baseline = CreatePull(includeDeath: false);
        var player = baseline.Actors.Single(actor => actor.Kind == ActorKind.Player);
        var boss = baseline.Actors.Single(actor => actor.Kind == ActorKind.Enemy);
        var provenance = new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = "workspace-dnc-test",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };
        var pull = baseline with
        {
            Actors = baseline.Actors.Select(actor => actor.Id == player.Id
                ? actor with { JobAbbreviation = "DNC" }
                : actor).ToArray(),
            Events =
            [
                new ActionUseEvent
                {
                    Id = new EventId(1),
                    Sequence = 1,
                    PullTime = TimeSpan.FromSeconds(5),
                    SourceActorId = player.Id,
                    TargetActorId = boss.Id,
                    Provenance = provenance,
                    ActionId = 15997,
                },
                new ActionUseEvent
                {
                    Id = new EventId(2),
                    Sequence = 2,
                    PullTime = TimeSpan.FromSeconds(6),
                    SourceActorId = player.Id,
                    TargetActorId = boss.Id,
                    Provenance = provenance,
                    ActionId = 15999,
                },
                new ActionUseEvent
                {
                    Id = new EventId(3),
                    Sequence = 3,
                    PullTime = TimeSpan.FromSeconds(7),
                    SourceActorId = player.Id,
                    TargetActorId = boss.Id,
                    Provenance = provenance,
                    ActionId = 16191,
                },
            ],
        };
        var controller = AnalyzerWorkspaceDataController.CreateDefault(new FakePullStore(pull));

        var loaded = await controller.LoadPullAsync(pull.Id);

        Assert.NotNull(loaded);
        var jobResult = Assert.Single(loaded.Results, result => result.AnalyzerId == DancerCoreExecutionAnalyzer.AnalyzerId);
        Assert.Equal(AnalysisCategory.Job, jobResult.Category);
        Assert.Equal(player.Id, jobResult.Actors[0]);
        Assert.NotEmpty(jobResult.Evidence.SelectMany(evidence => evidence.EventIds));
        Assert.DoesNotContain(loaded.Skipped, skip => skip.AnalyzerId == DancerCoreExecutionAnalyzer.AnalyzerId);
        Assert.DoesNotContain(loaded.Skipped, skip => skip.AnalyzerId == DancerBurstAndUptimeAnalyzer.AnalyzerId);
        Assert.Contains(loaded.Skipped, skip => skip.AnalyzerId == ForsakenOpeningAssignmentAnalyzer.AnalyzerId);
    }

    [Fact]
    public async Task DefaultWorkspaceSurfacesNeutralHealingAnalysisWhenHealingEvidenceExists()
    {
        var baseline = CreatePull(includeDeath: false);
        var player = baseline.Actors.Single(actor => actor.Kind == ActorKind.Player).Id;
        var heal = new HealEvent
        {
            Id = new EventId(2),
            Sequence = 2,
            PullTime = TimeSpan.FromSeconds(10),
            SourceActorId = player,
            TargetActorId = player,
            Provenance = new EventProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = "workspace-test",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
            Amount = 50000,
            ActionId = 200,
        };
        var pull = baseline with { Events = baseline.Events.Append<NormalizedEvent>(heal).ToArray() };
        var controller = AnalyzerWorkspaceDataController.CreateDefault(new FakePullStore(pull));

        var loaded = await controller.LoadPullAsync(pull.Id);

        Assert.NotNull(loaded);
        var healing = Assert.Single(loaded.Results, result => result.AnalyzerId == HealingActivityAnalyzer.AnalyzerId);
        Assert.Equal(AnalysisSeverity.Info, healing.Severity);
        Assert.Equal(AnalysisCategory.Healing, healing.Category);
        Assert.Contains("neutral activity summary—not an overheal/waste judgment", healing.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingPullReturnsNullWithoutRunningAnalysis()
    {
        var pull = CreatePull(includeDeath: true);
        var store = new FakePullStore(pull) { ReturnMissingOnLoad = true };
        var controller = AnalyzerWorkspaceDataController.CreateDefault(store);

        var loaded = await controller.LoadPullAsync(pull.Id);

        Assert.Null(loaded);
        Assert.Equal(1, store.LoadCount);
    }

    private static RecordedPull CreatePull(bool includeDeath)
    {
        var pullId = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        var player = new ActorId(1);
        var boss = new ActorId(2);
        var provenance = new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = "workspace-test",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };
        var events = new List<NormalizedEvent>
        {
            new DamageEvent
            {
                Id = new EventId(1),
                Sequence = 1,
                PullTime = TimeSpan.FromSeconds(5),
                SourceActorId = boss,
                TargetActorId = player,
                Provenance = provenance,
                Amount = 1000,
                ActionId = 100,
            },
        };
        if (includeDeath)
        {
            events.Add(new DeathEvent
            {
                Id = new EventId(2),
                Sequence = 2,
                PullTime = TimeSpan.FromSeconds(6),
                SourceActorId = boss,
                TargetActorId = player,
                Provenance = provenance,
            });
        }

        return new RecordedPull
        {
            Id = pullId,
            Metadata = new PullMetadata
            {
                TerritoryId = 777,
                TerritoryName = "Workspace Test",
                Duration = TimeSpan.FromMinutes(2),
                StartedAt = new DateTimeOffset(2026, 8, 18, 18, 0, 0, TimeSpan.Zero),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord { Id = player, Name = "Player", Kind = ActorKind.Player },
                new ActorRecord { Id = boss, Name = "Boss", Kind = ActorKind.Enemy },
            ],
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = "workspace-test",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private sealed class FakePullStore(RecordedPull pull) : IPullStore
    {
        public int LastQueryLimit { get; private set; }

        public int LoadCount { get; private set; }

        public bool ReturnMissingOnLoad { get; init; }

        public Task SaveAsync(RecordedPull savedPull, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RecordedPull?> LoadAsync(PullId id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            return Task.FromResult<RecordedPull?>(ReturnMissingOnLoad ? null : pull);
        }

        public Task<IReadOnlyList<PullSummary>> QueryAsync(PullQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastQueryLimit = query.Limit;
            IReadOnlyList<PullSummary> summaries =
            [
                new PullSummary
                {
                    Id = pull.Id,
                    TerritoryId = pull.Metadata.TerritoryId,
                    TerritoryName = pull.Metadata.TerritoryName,
                    Duration = pull.Metadata.Duration,
                    StartedAt = pull.Metadata.StartedAt,
                    ActorCount = pull.Actors.Count,
                    EventCount = pull.Events.Count,
                    SourceKind = pull.Provenance.SourceKind,
                },
            ];
            return Task.FromResult(summaries);
        }

        public Task DeleteAsync(PullId id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
