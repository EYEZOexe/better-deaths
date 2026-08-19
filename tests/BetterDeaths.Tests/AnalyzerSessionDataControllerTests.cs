namespace BetterDeaths;

using BetterDeaths.Analysis.Encounters.DancingMadUltimate;
using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Sessions;
using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using BetterDeaths.Windows.Analyzer;
using System.Runtime.CompilerServices;

public sealed class AnalyzerSessionDataControllerTests
{
    [Fact]
    public async Task QueriesCompactSummariesBeforeSequentialFullPullLoads()
    {
        var pulls = Enumerable.Range(1, 3).Select(index => BasicPull(index, 777)).ToArray();
        var store = new TrackingPullStore(pulls);
        var controller = new AnalyzerSessionDataController(store, EmptyEngine());

        var loaded = await controller.LoadAsync(new AnalyzerSessionRequest { TerritoryId = 777, Limit = 25 });

        Assert.NotNull(loaded);
        Assert.Equal("query", store.Operations[0]);
        Assert.Equal(new[] { "load:2", "load:3", "load:4" }, store.Operations.Skip(1));
        Assert.Equal(1, store.MaxConcurrentLoads);
        Assert.Equal(25, store.LastQueryLimit);
        Assert.Equal((uint)777, store.LastQueryTerritoryId);
        Assert.Equal(3, loaded.SelectedPullCount);
        Assert.Equal(3, loaded.Session.Pulls.Count);
        Assert.Empty(loaded.Diagnostics);
    }

    [Fact]
    public async Task SummaryTimeFilterRunsBeforeFullPullLoading()
    {
        var pulls = new[] { BasicPull(1, 777), BasicPull(2, 777), BasicPull(3, 777) };
        var store = new TrackingPullStore(pulls)
        {
            SummaryOverrides = new Dictionary<PullId, DateTimeOffset?>
            {
                [pulls[0].Id] = new DateTimeOffset(2026, 8, 19, 17, 0, 0, TimeSpan.Zero),
                [pulls[1].Id] = new DateTimeOffset(2026, 8, 19, 18, 0, 0, TimeSpan.Zero),
                [pulls[2].Id] = null,
            },
        };
        var controller = new AnalyzerSessionDataController(store, EmptyEngine());

        var loaded = await controller.LoadAsync(new AnalyzerSessionRequest
        {
            TerritoryId = 777,
            Limit = 50,
            From = new DateTimeOffset(2026, 8, 19, 17, 30, 0, TimeSpan.Zero),
            To = new DateTimeOffset(2026, 8, 19, 18, 30, 0, TimeSpan.Zero),
        });

        Assert.NotNull(loaded);
        Assert.Equal(new[] { pulls[1].Id }, store.LoadedPullIds);
        Assert.Single(loaded.Session.Pulls);
        Assert.Equal(pulls[1].Id, loaded.Session.Pulls[0].PullId);
    }

    [Fact]
    public async Task MissingAndFailedPullLoadsAreIsolatedFromRemainingSession()
    {
        var pulls = new[] { BasicPull(1, 777), BasicPull(2, 777), BasicPull(3, 777) };
        var store = new TrackingPullStore(pulls)
        {
            MissingPullIds = new HashSet<PullId> { pulls[1].Id },
            ThrowingPullIds = new HashSet<PullId> { pulls[2].Id },
        };
        var controller = new AnalyzerSessionDataController(store, EmptyEngine());

        var loaded = await controller.LoadAsync(new AnalyzerSessionRequest { TerritoryId = 777 });

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded.SelectedPullCount);
        Assert.Single(loaded.Session.Pulls);
        Assert.Equal(pulls[0].Id, loaded.Session.Pulls[0].PullId);
        Assert.Contains(loaded.Diagnostics, diagnostic =>
            diagnostic.PullId == pulls[1].Id && diagnostic.Kind == AnalyzerSessionDiagnosticKind.MissingPull);
        Assert.Contains(loaded.Diagnostics, diagnostic =>
            diagnostic.PullId == pulls[2].Id && diagnostic.Kind == AnalyzerSessionDiagnosticKind.PullLoadFailure);
    }

    [Fact]
    public async Task AnalyzerModuleFailureIsDiagnosticWhileSuccessfulModuleResultsRemainUsable()
    {
        var pull = BasicPull(1, 777);
        var registry = new AnalyzerRegistry();
        registry.Register(new SessionTestFindingAnalyzer());
        registry.Register(new SessionTestThrowingAnalyzer());
        var controller = new AnalyzerSessionDataController(
            new TrackingPullStore([pull]),
            new AnalyzerEngine(registry),
            new SessionTestEnricher());

        var loaded = await controller.LoadAsync(new AnalyzerSessionRequest { TerritoryId = 777 });

        Assert.NotNull(loaded);
        var sessionPull = Assert.Single(loaded.Session.Pulls);
        var result = Assert.Single(sessionPull.Results);
        Assert.Equal(SessionTestFindingAnalyzer.AnalyzerId, result.AnalyzerId);
        Assert.Contains(loaded.Diagnostics, diagnostic =>
            diagnostic.Kind == AnalyzerSessionDiagnosticKind.AnalyzerFailure &&
            diagnostic.AnalyzerId == SessionTestThrowingAnalyzer.AnalyzerId);
        var recurrence = Assert.Single(loaded.Analysis.Recurrences);
        Assert.Equal(1, recurrence.Counts.FindingCount);
        Assert.Equal(1, recurrence.Counts.OpportunityCount);
    }

    [Fact]
    public async Task CustomEnricherCanSupplyStableParticipantAndOpportunityEvidence()
    {
        var player = new ActorId(1);
        var pull = BasicPull(1, 777) with
        {
            Actors = [new ActorRecord { Id = player, Name = "Player", Kind = ActorKind.Player, JobAbbreviation = "DNC" }],
        };
        var registry = new AnalyzerRegistry();
        registry.Register(new SessionTestFindingAnalyzer(player));
        var participant = new SessionParticipantKey("static-player-1");
        var enricher = new SessionTestEnricher(participant, player);
        var controller = new AnalyzerSessionDataController(
            new TrackingPullStore([pull]),
            new AnalyzerEngine(registry),
            enricher);

        var loaded = await controller.LoadAsync(new AnalyzerSessionRequest { TerritoryId = 777 });

        Assert.NotNull(loaded);
        var recurrence = Assert.Single(loaded.Analysis.Recurrences);
        Assert.Equal(participant, recurrence.Key.ParticipantKey);
        Assert.Equal(participant, Assert.Single(recurrence.Evidence).ParticipantKey);
        var sessionParticipant = Assert.Single(loaded.Session.Participants);
        Assert.Equal(participant, sessionParticipant.Key);
        Assert.Equal("Player", sessionParticipant.DisplayName);
    }

    [Fact]
    public async Task DefaultEnricherDerivesOnlyEvidenceSafeForsakenOpportunityAndPhaseReach()
    {
        var pull = ForsakenCompatiblePull();
        var controller = AnalyzerSessionDataController.CreateDefault(new TrackingPullStore([pull]));

        var loaded = await controller.LoadAsync(new AnalyzerSessionRequest { TerritoryId = 1363 });

        Assert.NotNull(loaded);
        var sessionPull = Assert.Single(loaded.Session.Pulls);
        var opportunity = Assert.Single(sessionPull.Opportunities);
        Assert.Equal(ForsakenOpeningAssignmentAnalyzer.AnalyzerId, opportunity.Key.AnalyzerId);
        Assert.Equal(ForsakenOpeningAssignmentAnalyzer.IncompatibleAssignmentRuleKey, opportunity.Key.RuleKey);
        Assert.Equal(SessionOpportunityState.Evaluable, opportunity.State);
        var phase = Assert.Single(sessionPull.Progress);
        Assert.Equal(ForsakenDefinition.PhaseKey, phase.PhaseKey);
        Assert.Equal(2, phase.PhaseOrder);

        var failureRecurrence = Assert.Single(loaded.Analysis.Recurrences, row =>
            row.Key.FindingKey == opportunity.Key);
        Assert.Equal(0, failureRecurrence.Counts.FindingCount);
        Assert.Equal(1, failureRecurrence.Counts.OpportunityCount);
        Assert.Equal(0.0, failureRecurrence.Counts.Rate);
    }

    [Fact]
    public async Task SampledForsakenEvidenceRemainsUnknownForFailureOpportunity()
    {
        var pull = ForsakenCompatiblePull(CaptureFidelity.Sampled);
        var controller = AnalyzerSessionDataController.CreateDefault(new TrackingPullStore([pull]));

        var loaded = await controller.LoadAsync(new AnalyzerSessionRequest { TerritoryId = 1363 });

        Assert.NotNull(loaded);
        var opportunity = Assert.Single(Assert.Single(loaded.Session.Pulls).Opportunities);
        Assert.Equal(SessionOpportunityState.Unknown, opportunity.State);
        var recurrence = Assert.Single(loaded.Analysis.Recurrences, row => row.Key.FindingKey == opportunity.Key);
        Assert.Equal(0, recurrence.Counts.OpportunityCount);
        Assert.Equal(1, recurrence.Counts.UnknownCount);
        Assert.Null(recurrence.Counts.Rate);
    }

    [Fact]
    public async Task NewerLoadGenerationMakesOlderInFlightResultStale()
    {
        var pull = BasicPull(1, 777);
        var store = new BlockingFirstLoadPullStore(pull);
        var controller = new AnalyzerSessionDataController(store, EmptyEngine());
        var request = new AnalyzerSessionRequest { TerritoryId = 777 };

        var firstTask = controller.LoadAsync(request);
        await store.FirstLoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondTask = controller.LoadAsync(request);
        await store.SecondLoadCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        store.ReleaseFirstLoad.TrySetResult(true);

        var first = await firstTask;
        var second = await secondTask;

        Assert.Null(first);
        Assert.NotNull(second);
    }

    [Fact]
    public async Task ExplicitInvalidationMakesInFlightLoadStaleWithoutPublishingIt()
    {
        var pull = BasicPull(1, 777);
        var store = new BlockingFirstLoadPullStore(pull);
        var controller = new AnalyzerSessionDataController(store, EmptyEngine());
        var task = controller.LoadAsync(new AnalyzerSessionRequest { TerritoryId = 777 });
        await store.FirstLoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        controller.InvalidatePendingLoad();
        store.ReleaseFirstLoad.TrySetResult(true);

        Assert.Null(await task);
    }

    [Fact]
    public void SessionControllerDoesNotRenderOrRetainFullRecordedPullsInPublishedResult()
    {
        var source = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/AnalyzerSessionDataController.cs");

        Assert.DoesNotContain("ImGui", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecapWindow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FFLogs", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public required RecordedPull Pull", source, StringComparison.Ordinal);
        Assert.Contains("await pullStore.QueryAsync", source, StringComparison.Ordinal);
        Assert.Contains("await pullStore.LoadAsync", source, StringComparison.Ordinal);
        Assert.Contains("await analyzerEngine.AnalyzeAsync", source, StringComparison.Ordinal);
        Assert.Contains("IsCurrent(loadGeneration)", source, StringComparison.Ordinal);
    }

    private static AnalyzerEngine EmptyEngine()
    {
        return new AnalyzerEngine(new AnalyzerRegistry());
    }

    private static RecordedPull BasicPull(int index, uint territoryId)
    {
        return new RecordedPull
        {
            Id = PullId(index),
            Metadata = new PullMetadata
            {
                TerritoryId = territoryId,
                TerritoryName = $"Territory {territoryId}",
                Duration = TimeSpan.FromSeconds(90),
                StartedAt = new DateTimeOffset(2026, 8, 19, 18, 0, 0, TimeSpan.Zero).AddMinutes(index * 3),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors = [],
            Events = [],
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = $"test:session:{index}",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static RecordedPull ForsakenCompatiblePull(CaptureFidelity fidelity = CaptureFidelity.Exact)
    {
        var boss = new ActorId(99);
        var actors = new[]
        {
            Player(1, "Tank One", "PLD"), Player(2, "Tank Two", "WAR"),
            Player(3, "Healer One", "WHM"), Player(4, "Healer Two", "SCH"),
            Player(5, "Melee One", "DRG"), Player(6, "Melee Two", "VPR"),
            Player(7, "Ranged One", "BRD"), Player(8, "Ranged Two", "PCT"),
            new ActorRecord { Id = boss, Name = "Kefka", Kind = ActorKind.Enemy },
        };
        var statuses = new[]
        {
            (Actor: actors[0].Id, StatusId: 5086u), (Actor: actors[1].Id, StatusId: 5084u),
            (Actor: actors[2].Id, StatusId: 5086u), (Actor: actors[3].Id, StatusId: 5085u),
            (Actor: actors[4].Id, StatusId: 5085u), (Actor: actors[5].Id, StatusId: 5084u),
            (Actor: actors[6].Id, StatusId: 5085u), (Actor: actors[7].Id, StatusId: 5086u),
        };
        var provenance = new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = "test:session:forsaken",
            Fidelity = fidelity,
            Confidence = fidelity == CaptureFidelity.Exact ? 1.0f : 0.7f,
        };
        var events = statuses.Select((status, index) => (NormalizedEvent)new StatusApplyEvent
        {
            Id = new EventId(index + 1),
            Sequence = index + 1,
            PullTime = TimeSpan.FromSeconds(10 + index * 0.1),
            SourceActorId = boss,
            TargetActorId = status.Actor,
            Provenance = provenance,
            StatusId = status.StatusId,
            Duration = TimeSpan.FromSeconds(15),
        }).ToArray();

        return new RecordedPull
        {
            Id = PullId(100),
            Metadata = new PullMetadata
            {
                TerritoryId = 1363,
                TerritoryName = "Dancing Mad Ultimate",
                Duration = TimeSpan.FromSeconds(120),
                StartedAt = new DateTimeOffset(2026, 8, 19, 18, 0, 0, TimeSpan.Zero),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors = actors,
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = "test:session:forsaken",
                Fidelity = fidelity,
                Confidence = fidelity == CaptureFidelity.Exact ? 1.0f : 0.7f,
            },
        };
    }

    private static ActorRecord Player(int id, string name, string job) => new()
    {
        Id = new ActorId(id),
        Name = name,
        Kind = ActorKind.Player,
        JobAbbreviation = job,
    };

    private static PullId PullId(int index) => new(Guid.Parse($"20000000-0000-0000-0000-{index + 1:D12}"));

    private sealed class TrackingPullStore : IPullStore
    {
        private readonly Dictionary<PullId, RecordedPull> pulls;
        private int concurrentLoads;

        public TrackingPullStore(IEnumerable<RecordedPull> pulls)
        {
            this.pulls = pulls.ToDictionary(pull => pull.Id);
        }

        public List<string> Operations { get; } = [];

        public List<PullId> LoadedPullIds { get; } = [];

        public int MaxConcurrentLoads { get; private set; }

        public int LastQueryLimit { get; private set; }

        public uint? LastQueryTerritoryId { get; private set; }

        public IReadOnlySet<PullId> MissingPullIds { get; init; } = new HashSet<PullId>();

        public IReadOnlySet<PullId> ThrowingPullIds { get; init; } = new HashSet<PullId>();

        public IReadOnlyDictionary<PullId, DateTimeOffset?> SummaryOverrides { get; init; } =
            new Dictionary<PullId, DateTimeOffset?>();

        public Task SaveAsync(RecordedPull pull, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<RecordedPull?> LoadAsync(PullId id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Operations.Add($"load:{IdNumber(id)}");
            LoadedPullIds.Add(id);
            concurrentLoads++;
            MaxConcurrentLoads = Math.Max(MaxConcurrentLoads, concurrentLoads);
            try
            {
                await Task.Yield();
                if (ThrowingPullIds.Contains(id))
                {
                    throw new InvalidDataException("synthetic load failure");
                }

                if (MissingPullIds.Contains(id))
                {
                    return null;
                }

                return pulls[id];
            }
            finally
            {
                concurrentLoads--;
            }
        }

        public Task<IReadOnlyList<PullSummary>> QueryAsync(PullQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Operations.Add("query");
            LastQueryLimit = query.Limit;
            LastQueryTerritoryId = query.TerritoryId;
            IReadOnlyList<PullSummary> summaries = pulls.Values
                .Where(pull => query.TerritoryId is null || pull.Metadata.TerritoryId == query.TerritoryId)
                .OrderBy(pull => pull.Metadata.StartedAt)
                .Take(query.Limit)
                .Select(pull => new PullSummary
                {
                    Id = pull.Id,
                    TerritoryId = pull.Metadata.TerritoryId,
                    TerritoryName = pull.Metadata.TerritoryName,
                    Duration = pull.Metadata.Duration,
                    StartedAt = SummaryOverrides.TryGetValue(pull.Id, out var overrideValue)
                        ? overrideValue
                        : pull.Metadata.StartedAt,
                    ActorCount = pull.Actors.Count,
                    EventCount = pull.Events.Count,
                    SourceKind = pull.Provenance.SourceKind,
                })
                .ToArray();
            return Task.FromResult(summaries);
        }

        public Task DeleteAsync(PullId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static int IdNumber(PullId id)
        {
            return int.Parse(id.Value.ToString("N")[20..]);
        }
    }

    private sealed class BlockingFirstLoadPullStore(RecordedPull pull) : IPullStore
    {
        private int loadCount;

        public TaskCompletionSource<bool> FirstLoadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseFirstLoad { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> SecondLoadCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SaveAsync(RecordedPull savedPull, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<RecordedPull?> LoadAsync(PullId id, CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref loadCount);
            if (call == 1)
            {
                FirstLoadStarted.TrySetResult(true);
                await ReleaseFirstLoad.Task.WaitAsync(cancellationToken);
            }
            else
            {
                SecondLoadCompleted.TrySetResult(true);
            }

            return pull;
        }

        public Task<IReadOnlyList<PullSummary>> QueryAsync(PullQuery query, CancellationToken cancellationToken = default)
        {
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

        public Task DeleteAsync(PullId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SessionTestFindingAnalyzer(ActorId? actorId = null) : IAnalyzerModule
    {
        public const string AnalyzerId = "test.session.finding";
        public const string RuleKey = "test.failure";

        public string Id => AnalyzerId;

        public AnalyzerScope Scope => AnalyzerScope.Generic;

        public IReadOnlyCollection<string> Dependencies => [];

        public bool Supports(AnalyzerContext context) => true;

        public ValueTask AnalyzeAsync(
            AnalyzerContext context,
            IAnalysisResultSink results,
            CancellationToken cancellationToken)
        {
            var actors = actorId is { } actor ? new[] { actor } : Array.Empty<ActorId>();
            results.Add(new AnalysisResult
            {
                Id = AnalysisResultId.New(),
                AnalyzerId = AnalyzerId,
                RuleKey = RuleKey,
                Severity = AnalysisSeverity.Warning,
                Category = AnalysisCategory.Job,
                Title = "Synthetic session finding",
                Summary = "Synthetic session finding",
                Actors = actors,
                Evidence = [],
            });
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SessionTestThrowingAnalyzer : IAnalyzerModule
    {
        public const string AnalyzerId = "test.session.throw";

        public string Id => AnalyzerId;

        public AnalyzerScope Scope => AnalyzerScope.Generic;

        public IReadOnlyCollection<string> Dependencies => [];

        public bool Supports(AnalyzerContext context) => true;

        public ValueTask AnalyzeAsync(
            AnalyzerContext context,
            IAnalysisResultSink results,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("synthetic analyzer failure");
        }
    }

    private sealed class SessionTestEnricher(
        SessionParticipantKey? participantKey = null,
        ActorId? actorId = null) : IAnalyzerSessionPullEnricher
    {
        public AnalyzerSessionPullEnrichment Enrich(RecordedPull pull, AnalyzerRunResult run)
        {
            var key = new SessionFindingKey(SessionTestFindingAnalyzer.AnalyzerId, SessionTestFindingAnalyzer.RuleKey);
            if (participantKey is not { } participant || actorId is not { } actor)
            {
                return new AnalyzerSessionPullEnrichment
                {
                    Opportunities =
                    [
                        new SessionRuleOpportunity { Key = key, State = SessionOpportunityState.Evaluable },
                    ],
                };
            }

            return new AnalyzerSessionPullEnrichment
            {
                Opportunities =
                [
                    new SessionRuleOpportunity
                    {
                        Key = key,
                        State = SessionOpportunityState.Evaluable,
                        ParticipantKey = participant,
                    },
                ],
                ParticipantKeys = new Dictionary<ActorId, SessionParticipantKey> { [actor] = participant },
                Participants =
                [
                    new SessionParticipant
                    {
                        Key = participant,
                        DisplayName = "Player",
                        JobAbbreviation = "DNC",
                    },
                ],
            };
        }
    }

    private static string ReadRepositoryFile(
        string relativePath,
        [CallerFilePath] string testSourcePath = "")
    {
        var testDirectory = Path.GetDirectoryName(testSourcePath)
            ?? throw new InvalidOperationException("Could not resolve test source directory.");
        var repositoryRoot = Path.GetFullPath(Path.Combine(testDirectory, "..", ".."));
        return File.ReadAllText(Path.Combine(
            repositoryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
