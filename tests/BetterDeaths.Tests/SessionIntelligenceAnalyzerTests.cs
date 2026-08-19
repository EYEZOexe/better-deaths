namespace BetterDeaths;

using BetterDeaths.Analysis.Sessions;
using BetterDeaths.Domain;
using System.Runtime.CompilerServices;

public sealed class SessionIntelligenceAnalyzerTests
{
    private static readonly SessionFindingKey MechanicFailure = new(
        "encounter.dmu.forsaken-opening",
        "opening-assignment.incompatible");

    [Fact]
    public void RecurrenceReportsFindingsOverKnownOpportunitiesAndPreservesUnknowns()
    {
        var pulls = new List<SessionPullAnalysis>();
        for (var index = 0; index < 13; index++)
        {
            var finding = index < 4
                ? Finding(index, MechanicFailure, AnalysisSeverity.Warning)
                : null;
            pulls.Add(Pull(
                index,
                results: finding is null ? [] : [finding],
                opportunities:
                [
                    Opportunity(
                        MechanicFailure,
                        index < 11 ? SessionOpportunityState.Evaluable : SessionOpportunityState.Unknown),
                ]));
        }

        var analysis = SessionIntelligenceAnalyzer.Analyze(Session(pulls));

        var recurrence = Assert.Single(analysis.Recurrences);
        Assert.Equal(MechanicFailure, recurrence.Key.FindingKey);
        Assert.Null(recurrence.Key.ParticipantKey);
        Assert.Equal(4, recurrence.Counts.FindingCount);
        Assert.Equal(11, recurrence.Counts.OpportunityCount);
        Assert.Equal(2, recurrence.Counts.UnknownCount);
        Assert.NotNull(recurrence.Counts.Rate);
        Assert.Equal(4d / 11d, recurrence.Counts.Rate!.Value, 8);
        Assert.Equal(4, recurrence.Evidence.Count);
        Assert.Equal(AnalysisSeverity.Warning, recurrence.HighestFindingSeverity);
    }

    [Fact]
    public void ConcreteFindingProvesOnlyItsOwnOpportunityAndNeverInventsSuccessfulPasses()
    {
        var finding = Finding(1, MechanicFailure, AnalysisSeverity.Warning);
        var analysis = SessionIntelligenceAnalyzer.Analyze(Session(
        [
            Pull(1, results: [finding]),
        ]));

        var recurrence = Assert.Single(analysis.Recurrences);
        Assert.Equal(1, recurrence.Counts.FindingCount);
        Assert.Equal(1, recurrence.Counts.OpportunityCount);
        Assert.Equal(1.0, recurrence.Counts.Rate);
        Assert.Equal(0, recurrence.Counts.UnknownCount);
    }

    [Fact]
    public void StableParticipantDimensionSeparatesPlayerAndPartyRecurrence()
    {
        var player = new ActorId(1);
        var other = new ActorId(2);
        var participant = new SessionParticipantKey("ryujin");
        var playerFinding = Finding(1, MechanicFailure, AnalysisSeverity.Warning) with
        {
            Actors = [player],
        };
        var partyFinding = Finding(2, MechanicFailure, AnalysisSeverity.Warning) with
        {
            Actors = [player, other],
        };
        var pull = Pull(
            1,
            results: [playerFinding, partyFinding],
            opportunities:
            [
                Opportunity(MechanicFailure, SessionOpportunityState.Evaluable, participant),
                Opportunity(MechanicFailure, SessionOpportunityState.Evaluable),
            ],
            participantKeys: new Dictionary<ActorId, SessionParticipantKey>
            {
                [player] = participant,
                [other] = new SessionParticipantKey("other"),
            });

        var analysis = SessionIntelligenceAnalyzer.Analyze(Session([pull]));

        Assert.Equal(2, analysis.Recurrences.Count);
        var playerRecurrence = Assert.Single(analysis.Recurrences, row => row.Key.ParticipantKey == participant);
        var partyRecurrence = Assert.Single(analysis.Recurrences, row => row.Key.ParticipantKey is null);
        Assert.Equal(1, playerRecurrence.Counts.FindingCount);
        Assert.Equal(1, playerRecurrence.Counts.OpportunityCount);
        Assert.Equal(1, partyRecurrence.Counts.FindingCount);
        Assert.Equal(1, partyRecurrence.Counts.OpportunityCount);
        Assert.Equal(participant, Assert.Single(playerRecurrence.Evidence).ParticipantKey);
    }

    [Fact]
    public void WipeCausesUseOnlyExplicitSevereCauseReferencesAndLeaveMissingCauseUnknown()
    {
        var firstCause = Finding(1, MechanicFailure, AnalysisSeverity.Warning);
        var secondCause = Finding(2, MechanicFailure, AnalysisSeverity.Error);
        var unrelatedDeath = Finding(
            3,
            new SessionFindingKey("generic.death-context", "death.observed"),
            AnalysisSeverity.Error);
        var pulls = new[]
        {
            Pull(1, results: [firstCause], outcome: Wipe(firstCause.Id)),
            Pull(2, results: [secondCause], outcome: Wipe(secondCause.Id)),
            Pull(3, results: [unrelatedDeath], outcome: Wipe(causeResultId: null)),
        };

        var analysis = SessionIntelligenceAnalyzer.Analyze(Session(pulls));

        Assert.Equal(3, analysis.WipeCauses.TotalWipes);
        Assert.Equal(2, analysis.WipeCauses.KnownCauseWipes);
        Assert.Equal(1, analysis.WipeCauses.UnknownCauseWipes);
        var cause = Assert.Single(analysis.WipeCauses.Causes);
        Assert.Equal(MechanicFailure, cause.Key.FindingKey);
        Assert.Equal(2, cause.WipeCount);
        Assert.Equal(2, cause.Evidence.Count);
        Assert.DoesNotContain(cause.Evidence, evidence => evidence.ResultId == unrelatedDeath.Id);
        Assert.Equal(0, analysis.Diagnostics.InvalidWipeCauseReferenceCount);
    }

    [Fact]
    public void InvalidOrNonActionableWipeCauseReferenceStaysUnknownInsteadOfGuessing()
    {
        var neutral = Finding(1, MechanicFailure, AnalysisSeverity.Info);
        var analysis = SessionIntelligenceAnalyzer.Analyze(Session(
        [
            Pull(1, results: [neutral], outcome: Wipe(neutral.Id)),
        ]));

        Assert.Equal(1, analysis.WipeCauses.TotalWipes);
        Assert.Equal(0, analysis.WipeCauses.KnownCauseWipes);
        Assert.Equal(1, analysis.WipeCauses.UnknownCauseWipes);
        Assert.Empty(analysis.WipeCauses.Causes);
        Assert.Equal(1, analysis.Diagnostics.InvalidWipeCauseReferenceCount);
    }

    [Fact]
    public void ProgressionUsesExplicitPhaseEvidenceAndReportsUnknownPullsSeparately()
    {
        var pulls = new[]
        {
            Pull(1, progress: [Phase("p1", 1, 10)]),
            Pull(2, progress: [Phase("p1", 1, 10), Phase("p2", 2, 45)]),
            Pull(3, progress: [Phase("p1", 1, 10), Phase("p2", 2, 44), Phase("p3", 3, 80)]),
            Pull(4),
        };

        var progression = SessionIntelligenceAnalyzer.Analyze(Session(pulls)).Progression;

        Assert.Equal(4, progression.TotalPullCount);
        Assert.Equal(3, progression.EvaluablePullCount);
        Assert.Equal(1, progression.UnknownPullCount);
        Assert.Equal("p3", progression.FurthestPhaseReached?.PhaseKey);
        Assert.Equal(3, progression.FurthestPhaseReached?.PhaseOrder);

        var p1 = Assert.Single(progression.Phases, phase => phase.PhaseKey == "p1");
        var p2 = Assert.Single(progression.Phases, phase => phase.PhaseKey == "p2");
        var p3 = Assert.Single(progression.Phases, phase => phase.PhaseKey == "p3");
        Assert.Equal(3, p1.ReachedPullCount);
        Assert.Equal(1.0, p1.ReachRate);
        Assert.Equal(2, p2.ReachedPullCount);
        Assert.Equal(2d / 3d, p2.ReachRate!.Value, 8);
        Assert.Equal(TimeSpan.FromSeconds(44.5), p2.AverageReachedAt);
        Assert.Equal(1, p3.ReachedPullCount);
    }

    [Fact]
    public void RecentTrendUsesOpportunityRatesAndIsDeterministicUnderPullReordering()
    {
        var pulls = Enumerable.Range(0, 10)
            .Select(index => Pull(
                index,
                results: index < 4 || index == 5
                    ? [Finding(index, MechanicFailure, AnalysisSeverity.Warning)]
                    : [],
                opportunities: [Opportunity(MechanicFailure, SessionOpportunityState.Evaluable)]))
            .ToArray();
        var config = new SessionIntelligenceConfiguration
        {
            RecentPullCount = 5,
            MinimumTrendOpportunitiesPerWindow = 3,
            StableRateDelta = 0.05,
        };

        var forward = SessionIntelligenceAnalyzer.Analyze(Session(pulls), config);
        var reversed = SessionIntelligenceAnalyzer.Analyze(Session(pulls.Reverse().ToArray()), config);

        var forwardTrend = Assert.Single(forward.Trends);
        var reversedTrend = Assert.Single(reversed.Trends);
        Assert.Equal(SessionTrendDirection.Improving, forwardTrend.Direction);
        Assert.Equal(4, forwardTrend.Prior.FindingCount);
        Assert.Equal(5, forwardTrend.Prior.OpportunityCount);
        Assert.Equal(1, forwardTrend.Recent.FindingCount);
        Assert.Equal(5, forwardTrend.Recent.OpportunityCount);
        Assert.Equal(-0.6, forwardTrend.RateDelta!.Value, 8);
        Assert.Equal(forwardTrend, reversedTrend);
        Assert.Equal(forward.Recurrences.Single().Counts, reversed.Recurrences.Single().Counts);
    }

    [Fact]
    public void TrendRemainsInsufficientWhenKnownOpportunitySamplesAreTooSmall()
    {
        var pulls = Enumerable.Range(0, 4)
            .Select(index => Pull(
                index,
                results: index == 0 ? [Finding(index, MechanicFailure, AnalysisSeverity.Warning)] : [],
                opportunities: [Opportunity(MechanicFailure, SessionOpportunityState.Evaluable)]))
            .ToArray();
        var config = new SessionIntelligenceConfiguration
        {
            RecentPullCount = 2,
            MinimumTrendOpportunitiesPerWindow = 3,
        };

        var trend = Assert.Single(SessionIntelligenceAnalyzer.Analyze(Session(pulls), config).Trends);

        Assert.Equal(SessionTrendDirection.InsufficientEvidence, trend.Direction);
    }

    [Fact]
    public void UnkeyedActionableResultsAreMarkedDiagnosticAndExcludedFromRecurrence()
    {
        var unkeyed = Finding(1, MechanicFailure, AnalysisSeverity.Warning) with { RuleKey = null };

        var analysis = SessionIntelligenceAnalyzer.Analyze(Session([Pull(1, results: [unkeyed])]));

        Assert.Empty(analysis.Recurrences);
        Assert.Equal(1, analysis.Diagnostics.UnkeyedActionableResultCount);
    }

    [Fact]
    public void SessionAnalysisIsPureAndDoesNotReachIntoSourcePersistenceOrUi()
    {
        var source = ReadRepositoryFile("BetterDeaths/Analysis/Sessions/SessionIntelligenceAnalyzer.cs");

        foreach (var forbidden in new[]
                 {
                     "IPullStore",
                     "PullSummary",
                     "FFLogs",
                     "Dalamud",
                     "ImGui",
                     "HttpClient",
                     "AnalyzerEngine",
                     "AnalyzerContext",
                     "RecapWindow",
                 })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(".Title", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Summary", source, StringComparison.Ordinal);
        Assert.Contains("SessionFindingKey.TryCreate", source, StringComparison.Ordinal);
        Assert.Contains("CauseResultId", source, StringComparison.Ordinal);
    }

    private static RaidSession Session(IEnumerable<SessionPullAnalysis> pulls) => new()
    {
        Id = new RaidSessionId(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001")),
        TerritoryId = 1363,
        TerritoryName = "Dancing Mad Ultimate",
        Pulls = pulls.ToArray(),
    };

    private static SessionPullAnalysis Pull(
        int index,
        IReadOnlyList<AnalysisResult>? results = null,
        IReadOnlyList<SessionRuleOpportunity>? opportunities = null,
        IReadOnlyList<SessionProgressObservation>? progress = null,
        SessionPullOutcome? outcome = null,
        IReadOnlyDictionary<ActorId, SessionParticipantKey>? participantKeys = null)
    {
        return new SessionPullAnalysis
        {
            PullId = PullId(index),
            TerritoryId = 1363,
            TerritoryName = "Dancing Mad Ultimate",
            Duration = TimeSpan.FromSeconds(120),
            StartedAt = new DateTimeOffset(2026, 8, 19, 18, 0, 0, TimeSpan.Zero).AddMinutes(index * 3),
            Results = results ?? [],
            Opportunities = opportunities ?? [],
            Progress = progress ?? [],
            Outcome = outcome ?? new SessionPullOutcome { Kind = SessionPullOutcomeKind.Unknown },
            ParticipantKeys = participantKeys ?? new Dictionary<ActorId, SessionParticipantKey>(),
        };
    }

    private static SessionRuleOpportunity Opportunity(
        SessionFindingKey key,
        SessionOpportunityState state,
        SessionParticipantKey? participantKey = null) => new()
    {
        Key = key,
        State = state,
        ParticipantKey = participantKey,
    };

    private static SessionProgressObservation Phase(string key, int order, double seconds) => new()
    {
        PhaseKey = key,
        PhaseOrder = order,
        ReachedAt = TimeSpan.FromSeconds(seconds),
    };

    private static SessionPullOutcome Wipe(AnalysisResultId? causeResultId) => new()
    {
        Kind = SessionPullOutcomeKind.Wipe,
        CauseResultId = causeResultId,
    };

    private static AnalysisResult Finding(
        int index,
        SessionFindingKey key,
        AnalysisSeverity severity) => new()
    {
        Id = ResultId(index),
        AnalyzerId = key.AnalyzerId,
        RuleKey = key.RuleKey,
        Severity = severity,
        Category = AnalysisCategory.Mechanic,
        Title = $"Display title {index}",
        Summary = $"Display summary {index}",
        TimeRange = new TimeRange(TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(101)),
        Evidence = [],
        Confidence = 1.0f,
    };

    private static PullId PullId(int index) => new(Guid.Parse($"00000000-0000-0000-0000-{index + 1:D12}"));

    private static AnalysisResultId ResultId(int index) =>
        new(Guid.Parse($"10000000-0000-0000-0000-{index + 1:D12}"));

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
