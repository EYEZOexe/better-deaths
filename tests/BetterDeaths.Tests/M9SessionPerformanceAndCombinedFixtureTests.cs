namespace BetterDeaths;

using BetterDeaths.Analysis.Encounters.DancingMadUltimate;
using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Jobs.Dancer;
using BetterDeaths.Analysis.Sessions;
using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using BetterDeaths.Windows.Analyzer;
using System.Diagnostics;

public sealed class M9SessionPerformanceAndCombinedFixtureTests
{
    private static readonly SessionFindingKey ForsakenFailureKey = new(
        ForsakenOpeningAssignmentAnalyzer.AnalyzerId,
        ForsakenOpeningAssignmentAnalyzer.IncompatibleAssignmentRuleKey);

    private static readonly SessionFindingKey DancerExecutionKey = new(
        DancerCoreExecutionAnalyzer.AnalyzerId,
        DancerCoreExecutionAnalyzer.StandardDanceUnderstepRuleKey);

    private static readonly SessionParticipantKey DancerParticipant = new("static:dancer");
    private static readonly ActorId DancerActor = new(1);

    [Fact]
    public void FiveHundredPullCombinedSessionProducesOpportunityNormalizedEvidenceBackedIntelligenceWithinBudget()
    {
        var session = CombinedSession(500);
        var configuration = new SessionIntelligenceConfiguration
        {
            RecentPullCount = 50,
            MinimumTrendOpportunitiesPerWindow = 10,
            StableRateDelta = 0.03,
        };

        var stopwatch = Stopwatch.StartNew();
        var analysis = SessionIntelligenceAnalyzer.Analyze(session, configuration);
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"500-pull pure Session Intelligence took {stopwatch.Elapsed.TotalMilliseconds:F1}ms; expected an interactive-scale result under the generous 5s CI budget.");

        var mechanic = Assert.Single(analysis.Recurrences, row =>
            row.Key.FindingKey == ForsakenFailureKey && row.Key.ParticipantKey is null);
        Assert.Equal(100, mechanic.Counts.FindingCount);
        Assert.Equal(450, mechanic.Counts.OpportunityCount);
        Assert.Equal(50, mechanic.Counts.UnknownCount);
        Assert.Equal(100d / 450d, mechanic.Counts.Rate!.Value, 8);
        Assert.Equal(100, mechanic.Evidence.Count);
        Assert.All(mechanic.Evidence, evidence =>
        {
            Assert.Equal(ForsakenFailureKey, evidence.FindingKey);
            Assert.NotEqual(Guid.Empty, evidence.PullId.Value);
            Assert.NotEqual(Guid.Empty, evidence.ResultId.Value);
        });

        var dancer = Assert.Single(analysis.Recurrences, row =>
            row.Key.FindingKey == DancerExecutionKey && row.Key.ParticipantKey == DancerParticipant);
        Assert.Equal(118, dancer.Counts.FindingCount);
        Assert.Equal(500, dancer.Counts.OpportunityCount);
        Assert.Equal(0, dancer.Counts.UnknownCount);
        Assert.Equal(118, dancer.Evidence.Count);
        Assert.All(dancer.Evidence, evidence => Assert.Equal(DancerParticipant, evidence.ParticipantKey));

        Assert.Equal(500, analysis.WipeCauses.TotalWipes);
        Assert.Equal(100, analysis.WipeCauses.KnownCauseWipes);
        Assert.Equal(400, analysis.WipeCauses.UnknownCauseWipes);
        var wipeCause = Assert.Single(analysis.WipeCauses.Causes);
        Assert.Equal(ForsakenFailureKey, wipeCause.Key.FindingKey);
        Assert.Equal(100, wipeCause.WipeCount);
        Assert.Equal(100, wipeCause.Evidence.Count);

        Assert.Equal(500, analysis.Progression.TotalPullCount);
        Assert.Equal(500, analysis.Progression.EvaluablePullCount);
        Assert.Equal(0, analysis.Progression.UnknownPullCount);
        Assert.Equal("p4", analysis.Progression.FurthestPhaseReached?.PhaseKey);
        Assert.Equal(4, analysis.Progression.FurthestPhaseReached?.PhaseOrder);
        Assert.Equal(500, Assert.Single(analysis.Progression.Phases, phase => phase.PhaseKey == "p1").ReachedPullCount);
        Assert.Equal(400, Assert.Single(analysis.Progression.Phases, phase => phase.PhaseKey == "p2").ReachedPullCount);
        Assert.Equal(250, Assert.Single(analysis.Progression.Phases, phase => phase.PhaseKey == "p3").ReachedPullCount);
        Assert.Equal(100, Assert.Single(analysis.Progression.Phases, phase => phase.PhaseKey == "p4").ReachedPullCount);

        var dancerTrend = Assert.Single(analysis.Trends, trend =>
            trend.Key.FindingKey == DancerExecutionKey && trend.Key.ParticipantKey == DancerParticipant);
        Assert.Equal(SessionTrendDirection.Improving, dancerTrend.Direction);
        Assert.Equal(13, dancerTrend.Prior.FindingCount);
        Assert.Equal(50, dancerTrend.Prior.OpportunityCount);
        Assert.Equal(5, dancerTrend.Recent.FindingCount);
        Assert.Equal(50, dancerTrend.Recent.OpportunityCount);
        Assert.True(dancerTrend.RateDelta < 0.0);

        Assert.Equal(0, analysis.Diagnostics.UnkeyedActionableResultCount);
        Assert.Equal(0, analysis.Diagnostics.InvalidWipeCauseReferenceCount);
    }

    [Fact]
    public async Task FiveHundredPullOrchestrationQueriesSummariesFirstStreamsFullPullsSequentiallyAndDoesNotPublishFullEvents()
    {
        const int pullCount = 500;
        var pulls = Enumerable.Range(0, pullCount)
            .Select(index => CanonicalPull(index, eventCount: 20))
            .ToArray();
        var store = new PerformancePullStore(pulls);
        var controller = new AnalyzerSessionDataController(
            store,
            new AnalyzerEngine(new AnalyzerRegistry()),
            new EmptySessionEnricher());

        var stopwatch = Stopwatch.StartNew();
        var loaded = await controller.LoadAsync(new AnalyzerSessionRequest
        {
            TerritoryId = 777,
            Limit = pullCount,
        });
        stopwatch.Stop();

        Assert.NotNull(loaded);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"500-pull session orchestration took {stopwatch.Elapsed.TotalMilliseconds:F1}ms; expected completion under the generous 10s CI budget.");
        Assert.Equal("query", store.Operations[0]);
        Assert.Equal(pullCount, store.LoadCount);
        Assert.Equal(1, store.MaxConcurrentLoads);
        Assert.Equal(pullCount, loaded.SelectedPullCount);
        Assert.Equal(pullCount, loaded.Session.Pulls.Count);
        Assert.Empty(loaded.Diagnostics);
        Assert.Empty(loaded.Analysis.Recurrences);
        Assert.All(loaded.Session.Pulls, pull =>
        {
            Assert.Equal(20, pull.Results.Count == 0 ? 20 : -1);
            Assert.Empty(pull.Results);
            Assert.Empty(pull.Opportunities);
        });

        var publishedType = typeof(AnalyzerSessionLoaded);
        Assert.DoesNotContain(
            publishedType.GetProperties(),
            property => property.PropertyType == typeof(RecordedPull) ||
                        property.PropertyType == typeof(IReadOnlyList<RecordedPull>));
    }

    [Fact]
    public void CombinedSessionOutputIsDeterministicUnderInputReordering()
    {
        var forwardSession = CombinedSession(100);
        var reversedSession = forwardSession with
        {
            Pulls = forwardSession.Pulls.Reverse().ToArray(),
        };
        var config = new SessionIntelligenceConfiguration
        {
            RecentPullCount = 20,
            MinimumTrendOpportunitiesPerWindow = 5,
        };

        var forward = SessionIntelligenceAnalyzer.Analyze(forwardSession, config);
        var reversed = SessionIntelligenceAnalyzer.Analyze(reversedSession, config);

        Assert.Equal(ProjectRecurrences(forward), ProjectRecurrences(reversed));
        Assert.Equal(ProjectWipeCauses(forward), ProjectWipeCauses(reversed));
        Assert.Equal(ProjectProgression(forward), ProjectProgression(reversed));
        Assert.Equal(ProjectTrends(forward), ProjectTrends(reversed));
    }

    private static RaidSession CombinedSession(int pullCount)
    {
        var pulls = Enumerable.Range(0, pullCount).Select(CombinedPull).ToArray();
        return new RaidSession
        {
            Id = new RaidSessionId(Guid.Parse("99999999-8888-7777-6666-555555555555")),
            TerritoryId = 1363,
            TerritoryName = "Dancing Mad Ultimate",
            StartedAt = pulls.First().StartedAt,
            EndedAt = pulls.Last().StartedAt + pulls.Last().Duration,
            Pulls = pulls,
            Participants =
            [
                new SessionParticipant
                {
                    Key = DancerParticipant,
                    DisplayName = "Dancer",
                    JobAbbreviation = "DNC",
                },
            ],
        };
    }

    private static SessionPullAnalysis CombinedPull(int index)
    {
        var mechanicOpportunityState = index % 10 == 9
            ? SessionOpportunityState.Unknown
            : SessionOpportunityState.Evaluable;
        var mechanicFails = index % 5 == 0;
        var dancerFails = index < 450
            ? index % 4 == 0
            : index % 10 == 0;

        var results = new List<AnalysisResult>();
        AnalysisResult? mechanicResult = null;
        if (mechanicFails)
        {
            mechanicResult = Finding(
                index,
                localResultIndex: 1,
                ForsakenFailureKey,
                AnalysisSeverity.Warning,
                actors: []);
            results.Add(mechanicResult);
        }

        if (dancerFails)
        {
            results.Add(Finding(
                index,
                localResultIndex: 2,
                DancerExecutionKey,
                AnalysisSeverity.Warning,
                actors: [DancerActor]));
        }

        var progress = new List<SessionProgressObservation>
        {
            Phase("p1", 1, 20),
        };
        if (index < 400)
        {
            progress.Add(Phase("p2", 2, 60));
        }
        if (index < 250)
        {
            progress.Add(Phase("p3", 3, 100));
        }
        if (index < 100)
        {
            progress.Add(Phase("p4", 4, 140));
        }

        return new SessionPullAnalysis
        {
            PullId = PullId(index),
            TerritoryId = 1363,
            TerritoryName = "Dancing Mad Ultimate",
            Duration = TimeSpan.FromMinutes(3),
            StartedAt = new DateTimeOffset(2026, 8, 19, 18, 0, 0, TimeSpan.Zero).AddMinutes(index * 4),
            Actors =
            [
                new ActorRecord
                {
                    Id = DancerActor,
                    Name = "Dancer",
                    Kind = ActorKind.Player,
                    JobAbbreviation = "DNC",
                },
            ],
            Results = results,
            Opportunities =
            [
                new SessionRuleOpportunity
                {
                    Key = ForsakenFailureKey,
                    State = mechanicOpportunityState,
                },
                new SessionRuleOpportunity
                {
                    Key = DancerExecutionKey,
                    State = SessionOpportunityState.Evaluable,
                    ParticipantKey = DancerParticipant,
                },
            ],
            Progress = progress,
            Outcome = new SessionPullOutcome
            {
                Kind = SessionPullOutcomeKind.Wipe,
                CauseResultId = mechanicResult?.Id,
            },
            ParticipantKeys = new Dictionary<ActorId, SessionParticipantKey>
            {
                [DancerActor] = DancerParticipant,
            },
        };
    }

    private static AnalysisResult Finding(
        int pullIndex,
        int localResultIndex,
        SessionFindingKey key,
        AnalysisSeverity severity,
        IReadOnlyList<ActorId> actors)
    {
        return new AnalysisResult
        {
            Id = ResultId(pullIndex, localResultIndex),
            AnalyzerId = key.AnalyzerId,
            RuleKey = key.RuleKey,
            Severity = severity,
            Category = key == ForsakenFailureKey ? AnalysisCategory.Mechanic : AnalysisCategory.Job,
            Title = $"Display-only title {pullIndex}:{localResultIndex}",
            Summary = $"Display-only summary {pullIndex}:{localResultIndex}",
            TimeRange = new TimeRange(TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(101)),
            Actors = actors,
            Evidence = [],
            Confidence = 1.0f,
        };
    }

    private static SessionProgressObservation Phase(string key, int order, double seconds) => new()
    {
        PhaseKey = key,
        PhaseOrder = order,
        ReachedAt = TimeSpan.FromSeconds(seconds),
    };

    private static RecordedPull CanonicalPull(int index, int eventCount)
    {
        var actor = new ActorId(1);
        var provenance = new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = $"perf:{index}",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };
        var events = Enumerable.Range(0, eventCount)
            .Select(eventIndex => (NormalizedEvent)new ActionUseEvent
            {
                Id = new EventId(eventIndex + 1),
                Sequence = eventIndex + 1,
                PullTime = TimeSpan.FromMilliseconds(eventIndex * 500),
                SourceActorId = actor,
                Provenance = provenance,
                ActionId = (uint)(1000 + eventIndex),
            })
            .ToArray();

        return new RecordedPull
        {
            Id = PullId(index),
            Metadata = new PullMetadata
            {
                TerritoryId = 777,
                TerritoryName = "Session Performance Fixture",
                Duration = TimeSpan.FromMinutes(2),
                StartedAt = new DateTimeOffset(2026, 8, 19, 18, 0, 0, TimeSpan.Zero).AddMinutes(index * 3),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord { Id = actor, Name = "Player", Kind = ActorKind.Player, JobAbbreviation = "BRD" },
            ],
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = $"perf:{index}",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static PullId PullId(int index) =>
        new(Guid.Parse($"50000000-0000-0000-0000-{index + 1:D12}"));

    private static AnalysisResultId ResultId(int pullIndex, int localResultIndex) =>
        new(Guid.Parse($"60000000-0000-{localResultIndex:D4}-0000-{pullIndex + 1:D12}"));

    private static IReadOnlyList<string> ProjectRecurrences(SessionIntelligenceResult result) =>
        result.Recurrences.Select(row =>
            $"{row.Key}|{row.Counts.FindingCount}|{row.Counts.OpportunityCount}|{row.Counts.UnknownCount}|{row.Counts.Rate:R}")
        .ToArray();

    private static IReadOnlyList<string> ProjectWipeCauses(SessionIntelligenceResult result) =>
        result.WipeCauses.Causes.Select(row => $"{row.Key}|{row.WipeCount}").ToArray();

    private static IReadOnlyList<string> ProjectProgression(SessionIntelligenceResult result) =>
        result.Progression.Phases.Select(row =>
            $"{row.PhaseKey}|{row.PhaseOrder}|{row.ReachedPullCount}|{row.EvaluablePullCount}|{row.UnknownPullCount}|{row.ReachRate:R}")
        .ToArray();

    private static IReadOnlyList<string> ProjectTrends(SessionIntelligenceResult result) =>
        result.Trends.Select(row =>
            $"{row.Key}|{row.Direction}|{row.Prior.FindingCount}/{row.Prior.OpportunityCount}|{row.Recent.FindingCount}/{row.Recent.OpportunityCount}")
        .ToArray();

    private sealed class PerformancePullStore(IEnumerable<RecordedPull> pulls) : IPullStore
    {
        private readonly Dictionary<PullId, RecordedPull> byId = pulls.ToDictionary(pull => pull.Id);
        private int concurrentLoads;

        public List<string> Operations { get; } = [];
        public int LoadCount { get; private set; }
        public int MaxConcurrentLoads { get; private set; }

        public Task SaveAsync(RecordedPull pull, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RecordedPull?> LoadAsync(PullId id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Operations.Add("load");
            LoadCount++;
            concurrentLoads++;
            MaxConcurrentLoads = Math.Max(MaxConcurrentLoads, concurrentLoads);
            var pull = byId[id];
            concurrentLoads--;
            return Task.FromResult<RecordedPull?>(pull);
        }

        public Task<IReadOnlyList<PullSummary>> QueryAsync(PullQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Operations.Add("query");
            IReadOnlyList<PullSummary> summaries = byId.Values
                .Where(pull => query.TerritoryId is null || pull.Metadata.TerritoryId == query.TerritoryId)
                .OrderBy(pull => pull.Metadata.StartedAt)
                .Take(query.Limit)
                .Select(pull => new PullSummary
                {
                    Id = pull.Id,
                    TerritoryId = pull.Metadata.TerritoryId,
                    TerritoryName = pull.Metadata.TerritoryName,
                    Duration = pull.Metadata.Duration,
                    StartedAt = pull.Metadata.StartedAt,
                    ActorCount = pull.Actors.Count,
                    EventCount = pull.Events.Count,
                    SourceKind = pull.Provenance.SourceKind,
                })
                .ToArray();
            return Task.FromResult(summaries);
        }

        public Task DeleteAsync(PullId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class EmptySessionEnricher : IAnalyzerSessionPullEnricher
    {
        public AnalyzerSessionPullEnrichment Enrich(RecordedPull pull, AnalyzerRunResult run) => new();
    }
}
