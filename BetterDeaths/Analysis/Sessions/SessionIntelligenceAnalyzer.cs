namespace BetterDeaths.Analysis.Sessions;

using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

internal readonly record struct SessionRecurrenceKey(
    SessionFindingKey FindingKey,
    SessionParticipantKey? ParticipantKey)
{
    public override string ToString() => ParticipantKey is { } participant
        ? $"{FindingKey}|participant:{participant.Value}"
        : $"{FindingKey}|party";
}

internal sealed record SessionRecurrenceSummary
{
    public required SessionRecurrenceKey Key { get; init; }

    public required SessionOccurrenceCounts Counts { get; init; }

    public AnalysisSeverity? HighestFindingSeverity { get; init; }

    public required IReadOnlyList<SessionEvidenceReference> Evidence { get; init; }
}

internal sealed record SessionWipeCauseEntry
{
    public required SessionRecurrenceKey Key { get; init; }

    public required int WipeCount { get; init; }

    public required IReadOnlyList<SessionEvidenceReference> Evidence { get; init; }
}

internal sealed record SessionWipeCauseSummary
{
    public required int TotalWipes { get; init; }

    public required int KnownCauseWipes { get; init; }

    public required int UnknownCauseWipes { get; init; }

    public required IReadOnlyList<SessionWipeCauseEntry> Causes { get; init; }
}

internal sealed record SessionPhaseReachSummary
{
    public required string PhaseKey { get; init; }

    public required int PhaseOrder { get; init; }

    public required int ReachedPullCount { get; init; }

    public required int EvaluablePullCount { get; init; }

    public required int UnknownPullCount { get; init; }

    public double? ReachRate => EvaluablePullCount == 0
        ? null
        : (double)ReachedPullCount / EvaluablePullCount;

    public TimeSpan? AverageReachedAt { get; init; }
}

internal sealed record SessionProgressionSummary
{
    public required int TotalPullCount { get; init; }

    public required int EvaluablePullCount { get; init; }

    public required int UnknownPullCount { get; init; }

    public SessionPhaseReachSummary? FurthestPhaseReached { get; init; }

    public required IReadOnlyList<SessionPhaseReachSummary> Phases { get; init; }
}

internal enum SessionTrendDirection
{
    InsufficientEvidence,
    Improving,
    Stable,
    Worsening,
}

internal sealed record SessionTrendSummary
{
    public required SessionRecurrenceKey Key { get; init; }

    public required SessionTrendDirection Direction { get; init; }

    public required SessionOccurrenceCounts Prior { get; init; }

    public required SessionOccurrenceCounts Recent { get; init; }

    public double? RateDelta => Prior.Rate is { } prior && Recent.Rate is { } recent
        ? recent - prior
        : null;
}

internal sealed record SessionAnalysisDiagnostics
{
    public required int UnkeyedActionableResultCount { get; init; }

    public required int InvalidWipeCauseReferenceCount { get; init; }
}

internal sealed record SessionIntelligenceResult
{
    public required IReadOnlyList<SessionRecurrenceSummary> Recurrences { get; init; }

    public required SessionWipeCauseSummary WipeCauses { get; init; }

    public required SessionProgressionSummary Progression { get; init; }

    public required IReadOnlyList<SessionTrendSummary> Trends { get; init; }

    public required SessionAnalysisDiagnostics Diagnostics { get; init; }
}

internal sealed record SessionIntelligenceConfiguration
{
    public int RecentPullCount { get; init; } = 5;

    public int MinimumTrendOpportunitiesPerWindow { get; init; } = 3;

    public double StableRateDelta { get; init; } = 0.05;

    public void Validate()
    {
        if (RecentPullCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RecentPullCount));
        }

        if (MinimumTrendOpportunitiesPerWindow <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumTrendOpportunitiesPerWindow));
        }

        if (!double.IsFinite(StableRateDelta) || StableRateDelta < 0.0 || StableRateDelta > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(StableRateDelta));
        }
    }
}

internal static class SessionIntelligenceAnalyzer
{
    private static readonly AnalysisSeverity ActionableSeverity = AnalysisSeverity.Optimization;

    public static SessionIntelligenceResult Analyze(
        RaidSession session,
        SessionIntelligenceConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        var config = configuration ?? new SessionIntelligenceConfiguration();
        config.Validate();
        ValidateSession(session);

        var orderedPulls = OrderPulls(session.Pulls);
        var pullFacts = new List<PullFacts>(orderedPulls.Count);
        var unkeyedActionable = 0;
        foreach (var pull in orderedPulls)
        {
            var facts = BuildPullFacts(pull, out var unkeyedForPull);
            pullFacts.Add(facts);
            unkeyedActionable += unkeyedForPull;
        }

        var recurrences = BuildRecurrences(pullFacts);
        var wipeCauses = BuildWipeCauseSummary(orderedPulls, out var invalidCauseReferences);
        var progression = BuildProgression(orderedPulls);
        var trends = BuildTrends(pullFacts, recurrences, config);

        return new SessionIntelligenceResult
        {
            Recurrences = recurrences,
            WipeCauses = wipeCauses,
            Progression = progression,
            Trends = trends,
            Diagnostics = new SessionAnalysisDiagnostics
            {
                UnkeyedActionableResultCount = unkeyedActionable,
                InvalidWipeCauseReferenceCount = invalidCauseReferences,
            },
        };
    }

    private static void ValidateSession(RaidSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(session.TerritoryName);
        if (session.TerritoryId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(session.TerritoryId));
        }

        if (session.EndedAt is { } endedAt && session.StartedAt is { } startedAt && endedAt < startedAt)
        {
            throw new ArgumentException("Raid session end cannot precede start.", nameof(session));
        }

        foreach (var pull in session.Pulls)
        {
            ArgumentNullException.ThrowIfNull(pull);
            if (pull.TerritoryId != session.TerritoryId)
            {
                throw new ArgumentException(
                    $"Pull '{pull.PullId.Value}' territory {pull.TerritoryId} does not match session territory {session.TerritoryId}.",
                    nameof(session));
            }

            if (pull.Duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(session), "Pull duration cannot be negative.");
            }

            foreach (var progress in pull.Progress)
            {
                ArgumentNullException.ThrowIfNull(progress);
                ArgumentException.ThrowIfNullOrWhiteSpace(progress.PhaseKey);
                if (progress.PhaseOrder < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(session), "Phase order cannot be negative.");
                }

                if (progress.ReachedAt is { } reachedAt && (reachedAt < TimeSpan.Zero || reachedAt > pull.Duration))
                {
                    throw new ArgumentOutOfRangeException(nameof(session), "Phase reach time must fall within the pull.");
                }
            }
        }
    }

    private static IReadOnlyList<SessionPullAnalysis> OrderPulls(IReadOnlyList<SessionPullAnalysis> pulls)
    {
        return pulls
            .OrderBy(pull => pull.StartedAt.HasValue ? 0 : 1)
            .ThenBy(pull => pull.StartedAt ?? DateTimeOffset.MaxValue)
            .ThenBy(pull => pull.PullId.Value)
            .ToArray();
    }

    private static PullFacts BuildPullFacts(SessionPullAnalysis pull, out int unkeyedActionable)
    {
        var facts = new Dictionary<SessionRecurrenceKey, MutableOccurrence>();
        foreach (var opportunity in pull.Opportunities)
        {
            ArgumentNullException.ThrowIfNull(opportunity);
            var key = new SessionRecurrenceKey(opportunity.Key, opportunity.ParticipantKey);
            var occurrence = GetOrCreate(facts, key);
            switch (opportunity.State)
            {
                case SessionOpportunityState.Evaluable:
                    occurrence.EvaluableOpportunities++;
                    break;
                case SessionOpportunityState.Unknown:
                    occurrence.UnknownOpportunities++;
                    break;
                case SessionOpportunityState.NotApplicable:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(opportunity.State));
            }
        }

        unkeyedActionable = 0;
        foreach (var result in pull.Results)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (result.Severity < ActionableSeverity)
            {
                continue;
            }

            if (!SessionFindingKey.TryCreate(result, out var findingKey))
            {
                unkeyedActionable++;
                continue;
            }

            var participantKey = ResolveParticipantKey(pull, result);
            var key = new SessionRecurrenceKey(findingKey, participantKey);
            var occurrence = GetOrCreate(facts, key);
            occurrence.FindingCount++;
            occurrence.HighestSeverity = occurrence.HighestSeverity is { } severity && severity > result.Severity
                ? severity
                : result.Severity;
            occurrence.Evidence.Add(ToEvidenceReference(pull, result, findingKey, participantKey));
        }

        foreach (var occurrence in facts.Values)
        {
            // A concrete finding proves at least one opportunity occurred even if an upstream
            // opportunity provider omitted it. This can increase only the known denominator to the
            // number of observed findings; it never fabricates a successful/pass opportunity.
            occurrence.EvaluableOpportunities = Math.Max(occurrence.EvaluableOpportunities, occurrence.FindingCount);
        }

        return new PullFacts(pull, facts);
    }

    private static SessionParticipantKey? ResolveParticipantKey(SessionPullAnalysis pull, AnalysisResult result)
    {
        var participants = result.Actors
            .Where(pull.ParticipantKeys.ContainsKey)
            .Select(actorId => pull.ParticipantKeys[actorId])
            .Distinct()
            .ToArray();
        return participants.Length == 1 ? participants[0] : null;
    }

    private static SessionEvidenceReference ToEvidenceReference(
        SessionPullAnalysis pull,
        AnalysisResult result,
        SessionFindingKey findingKey,
        SessionParticipantKey? participantKey)
    {
        return new SessionEvidenceReference
        {
            PullId = pull.PullId,
            ResultId = result.Id,
            FindingKey = findingKey,
            PullLocalActorIds = result.Actors,
            ParticipantKey = participantKey,
            TimeRange = result.TimeRange,
        };
    }

    private static IReadOnlyList<SessionRecurrenceSummary> BuildRecurrences(IReadOnlyList<PullFacts> pulls)
    {
        var aggregate = new Dictionary<SessionRecurrenceKey, MutableOccurrence>();
        foreach (var pull in pulls)
        {
            foreach (var (key, occurrence) in pull.Occurrences)
            {
                var combined = GetOrCreate(aggregate, key);
                combined.FindingCount += occurrence.FindingCount;
                combined.EvaluableOpportunities += occurrence.EvaluableOpportunities;
                combined.UnknownOpportunities += occurrence.UnknownOpportunities;
                if (occurrence.HighestSeverity is { } severity &&
                    (combined.HighestSeverity is null || severity > combined.HighestSeverity))
                {
                    combined.HighestSeverity = severity;
                }

                combined.Evidence.AddRange(occurrence.Evidence);
            }
        }

        return aggregate
            .Select(pair => new SessionRecurrenceSummary
            {
                Key = pair.Key,
                Counts = new SessionOccurrenceCounts(
                    pair.Value.FindingCount,
                    pair.Value.EvaluableOpportunities,
                    pair.Value.UnknownOpportunities),
                HighestFindingSeverity = pair.Value.HighestSeverity,
                Evidence = pair.Value.Evidence.ToArray(),
            })
            .OrderByDescending(summary => summary.Counts.Rate ?? -1.0)
            .ThenByDescending(summary => summary.Counts.OpportunityCount)
            .ThenBy(summary => summary.Key.FindingKey.AnalyzerId, StringComparer.Ordinal)
            .ThenBy(summary => summary.Key.FindingKey.RuleKey, StringComparer.Ordinal)
            .ThenBy(summary => summary.Key.ParticipantKey?.Value ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    private static SessionWipeCauseSummary BuildWipeCauseSummary(
        IReadOnlyList<SessionPullAnalysis> pulls,
        out int invalidCauseReferences)
    {
        var causes = new Dictionary<SessionRecurrenceKey, List<SessionEvidenceReference>>();
        var totalWipes = 0;
        var knownCauseWipes = 0;
        var unknownCauseWipes = 0;
        invalidCauseReferences = 0;

        foreach (var pull in pulls)
        {
            if (pull.Outcome.Kind != SessionPullOutcomeKind.Wipe)
            {
                continue;
            }

            totalWipes++;
            if (pull.Outcome.CauseResultId is not { } causeResultId)
            {
                unknownCauseWipes++;
                continue;
            }

            var cause = pull.Results.SingleOrDefault(result => result.Id == causeResultId);
            if (cause is null ||
                cause.Severity < AnalysisSeverity.Warning ||
                !SessionFindingKey.TryCreate(cause, out var findingKey))
            {
                invalidCauseReferences++;
                unknownCauseWipes++;
                continue;
            }

            var participantKey = ResolveParticipantKey(pull, cause);
            var recurrenceKey = new SessionRecurrenceKey(findingKey, participantKey);
            if (!causes.TryGetValue(recurrenceKey, out var evidence))
            {
                evidence = [];
                causes.Add(recurrenceKey, evidence);
            }

            evidence.Add(ToEvidenceReference(pull, cause, findingKey, participantKey));
            knownCauseWipes++;
        }

        var entries = causes
            .Select(pair => new SessionWipeCauseEntry
            {
                Key = pair.Key,
                WipeCount = pair.Value.Count,
                Evidence = pair.Value.ToArray(),
            })
            .OrderByDescending(entry => entry.WipeCount)
            .ThenBy(entry => entry.Key.FindingKey.AnalyzerId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.FindingKey.RuleKey, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.ParticipantKey?.Value ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        return new SessionWipeCauseSummary
        {
            TotalWipes = totalWipes,
            KnownCauseWipes = knownCauseWipes,
            UnknownCauseWipes = unknownCauseWipes,
            Causes = entries,
        };
    }

    private static SessionProgressionSummary BuildProgression(IReadOnlyList<SessionPullAnalysis> pulls)
    {
        var evaluablePulls = pulls.Where(pull => pull.Progress.Count > 0).ToArray();
        var unknownPullCount = pulls.Count - evaluablePulls.Length;
        var phaseGroups = new Dictionary<PhaseIdentity, MutablePhaseReach>();

        foreach (var pull in evaluablePulls)
        {
            foreach (var progress in pull.Progress
                         .GroupBy(item => new PhaseIdentity(item.PhaseKey.Trim(), item.PhaseOrder))
                         .Select(group => group.OrderBy(item => item.ReachedAt ?? TimeSpan.MaxValue).First()))
            {
                var identity = new PhaseIdentity(progress.PhaseKey.Trim(), progress.PhaseOrder);
                if (!phaseGroups.TryGetValue(identity, out var reach))
                {
                    reach = new MutablePhaseReach();
                    phaseGroups.Add(identity, reach);
                }

                reach.ReachedPullCount++;
                if (progress.ReachedAt is { } reachedAt)
                {
                    reach.ReachedAtTicks.Add(reachedAt.Ticks);
                }
            }
        }

        var phases = phaseGroups
            .Select(pair => new SessionPhaseReachSummary
            {
                PhaseKey = pair.Key.PhaseKey,
                PhaseOrder = pair.Key.PhaseOrder,
                ReachedPullCount = pair.Value.ReachedPullCount,
                EvaluablePullCount = evaluablePulls.Length,
                UnknownPullCount = unknownPullCount,
                AverageReachedAt = pair.Value.ReachedAtTicks.Count == 0
                    ? null
                    : TimeSpan.FromTicks((long)pair.Value.ReachedAtTicks.Average()),
            })
            .OrderBy(phase => phase.PhaseOrder)
            .ThenBy(phase => phase.PhaseKey, StringComparer.Ordinal)
            .ToArray();

        var furthest = phases
            .OrderByDescending(phase => phase.PhaseOrder)
            .ThenBy(phase => phase.PhaseKey, StringComparer.Ordinal)
            .FirstOrDefault();

        return new SessionProgressionSummary
        {
            TotalPullCount = pulls.Count,
            EvaluablePullCount = evaluablePulls.Length,
            UnknownPullCount = unknownPullCount,
            FurthestPhaseReached = furthest,
            Phases = phases,
        };
    }

    private static IReadOnlyList<SessionTrendSummary> BuildTrends(
        IReadOnlyList<PullFacts> pulls,
        IReadOnlyList<SessionRecurrenceSummary> recurrences,
        SessionIntelligenceConfiguration configuration)
    {
        if (pulls.Count < 2)
        {
            return [];
        }

        var recentCount = Math.Min(configuration.RecentPullCount, pulls.Count / 2);
        if (recentCount == 0)
        {
            return [];
        }

        var prior = pulls.Skip(Math.Max(0, pulls.Count - recentCount * 2)).Take(recentCount).ToArray();
        var recent = pulls.Skip(pulls.Count - recentCount).Take(recentCount).ToArray();
        return recurrences
            .Select(recurrence => BuildTrend(recurrence.Key, prior, recent, configuration))
            .OrderBy(trend => trend.Key.FindingKey.AnalyzerId, StringComparer.Ordinal)
            .ThenBy(trend => trend.Key.FindingKey.RuleKey, StringComparer.Ordinal)
            .ThenBy(trend => trend.Key.ParticipantKey?.Value ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    private static SessionTrendSummary BuildTrend(
        SessionRecurrenceKey key,
        IReadOnlyList<PullFacts> priorPulls,
        IReadOnlyList<PullFacts> recentPulls,
        SessionIntelligenceConfiguration configuration)
    {
        var prior = CountForKey(priorPulls, key);
        var recent = CountForKey(recentPulls, key);
        var direction = SessionTrendDirection.InsufficientEvidence;
        if (prior.OpportunityCount >= configuration.MinimumTrendOpportunitiesPerWindow &&
            recent.OpportunityCount >= configuration.MinimumTrendOpportunitiesPerWindow &&
            prior.Rate is { } priorRate && recent.Rate is { } recentRate)
        {
            var delta = recentRate - priorRate;
            direction = Math.Abs(delta) <= configuration.StableRateDelta
                ? SessionTrendDirection.Stable
                : delta < 0.0
                    ? SessionTrendDirection.Improving
                    : SessionTrendDirection.Worsening;
        }

        return new SessionTrendSummary
        {
            Key = key,
            Direction = direction,
            Prior = prior,
            Recent = recent,
        };
    }

    private static SessionOccurrenceCounts CountForKey(IReadOnlyList<PullFacts> pulls, SessionRecurrenceKey key)
    {
        var findings = 0;
        var opportunities = 0;
        var unknown = 0;
        foreach (var pull in pulls)
        {
            if (!pull.Occurrences.TryGetValue(key, out var occurrence))
            {
                continue;
            }

            findings += occurrence.FindingCount;
            opportunities += occurrence.EvaluableOpportunities;
            unknown += occurrence.UnknownOpportunities;
        }

        return new SessionOccurrenceCounts(findings, opportunities, unknown);
    }

    private static MutableOccurrence GetOrCreate(
        IDictionary<SessionRecurrenceKey, MutableOccurrence> occurrences,
        SessionRecurrenceKey key)
    {
        if (!occurrences.TryGetValue(key, out var occurrence))
        {
            occurrence = new MutableOccurrence();
            occurrences.Add(key, occurrence);
        }

        return occurrence;
    }

    private sealed class MutableOccurrence
    {
        public int FindingCount { get; set; }

        public int EvaluableOpportunities { get; set; }

        public int UnknownOpportunities { get; set; }

        public AnalysisSeverity? HighestSeverity { get; set; }

        public List<SessionEvidenceReference> Evidence { get; } = [];
    }

    private sealed record PullFacts(
        SessionPullAnalysis Pull,
        IReadOnlyDictionary<SessionRecurrenceKey, MutableOccurrence> Occurrences);

    private readonly record struct PhaseIdentity(string PhaseKey, int PhaseOrder);

    private sealed class MutablePhaseReach
    {
        public int ReachedPullCount { get; set; }

        public List<long> ReachedAtTicks { get; } = [];
    }
}
