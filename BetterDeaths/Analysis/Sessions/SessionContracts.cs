namespace BetterDeaths.Analysis.Sessions;

using BetterDeaths.Domain;
using System;
using System.Collections.Generic;

internal readonly record struct RaidSessionId
{
    public RaidSessionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Raid session identity cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }
}

internal readonly record struct SessionParticipantKey
{
    public SessionParticipantKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

internal readonly record struct SessionFindingKey
{
    public SessionFindingKey(string analyzerId, string ruleKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analyzerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleKey);
        AnalyzerId = analyzerId.Trim();
        RuleKey = ruleKey.Trim();
    }

    public string AnalyzerId { get; }

    public string RuleKey { get; }

    public static bool TryCreate(AnalysisResult result, out SessionFindingKey key)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(result.AnalyzerId) || string.IsNullOrWhiteSpace(result.RuleKey))
        {
            key = default;
            return false;
        }

        key = new SessionFindingKey(result.AnalyzerId, result.RuleKey);
        return true;
    }

    public override string ToString() => $"{AnalyzerId}:{RuleKey}";
}

internal enum SessionOpportunityState
{
    Unknown,
    Evaluable,
    NotApplicable,
}

internal sealed record SessionRuleOpportunity
{
    public required SessionFindingKey Key { get; init; }

    public required SessionOpportunityState State { get; init; }

    /// <summary>
    /// Optional stable participant dimension supplied only when the session assembler can resolve
    /// the same participant across pulls. Pull-local ActorId values are not session identities.
    /// </summary>
    public SessionParticipantKey? ParticipantKey { get; init; }
}

internal sealed record SessionEvidenceReference
{
    public required PullId PullId { get; init; }

    public required AnalysisResultId ResultId { get; init; }

    public required SessionFindingKey FindingKey { get; init; }

    public IReadOnlyList<ActorId> PullLocalActorIds { get; init; } = [];

    public SessionParticipantKey? ParticipantKey { get; init; }

    public TimeRange? TimeRange { get; init; }
}

internal sealed record SessionOccurrenceCounts
{
    public SessionOccurrenceCounts(int findingCount, int opportunityCount, int unknownCount)
    {
        if (findingCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(findingCount));
        }

        if (opportunityCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(opportunityCount));
        }

        if (unknownCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unknownCount));
        }

        if (findingCount > opportunityCount)
        {
            throw new ArgumentException("Finding count cannot exceed evaluated opportunity count.", nameof(findingCount));
        }

        FindingCount = findingCount;
        OpportunityCount = opportunityCount;
        UnknownCount = unknownCount;
    }

    public int FindingCount { get; }

    public int OpportunityCount { get; }

    public int UnknownCount { get; }

    public double? Rate => OpportunityCount == 0
        ? null
        : (double)FindingCount / OpportunityCount;
}

internal sealed record SessionParticipant
{
    public required SessionParticipantKey Key { get; init; }

    public required string DisplayName { get; init; }

    public string? JobAbbreviation { get; init; }
}

internal enum SessionPullOutcomeKind
{
    Unknown,
    Wipe,
    Kill,
}

internal sealed record SessionPullOutcome
{
    public required SessionPullOutcomeKind Kind { get; init; }

    /// <summary>
    /// Optional result explicitly established by upstream analysis/orchestration as the pull-ending
    /// cause. Session analysis never substitutes the last damage/death/result for a missing cause.
    /// </summary>
    public AnalysisResultId? CauseResultId { get; init; }
}

internal sealed record SessionProgressObservation
{
    public required string PhaseKey { get; init; }

    public required int PhaseOrder { get; init; }

    public TimeSpan? ReachedAt { get; init; }
}

internal sealed record SessionPullAnalysis
{
    public required PullId PullId { get; init; }

    public required uint TerritoryId { get; init; }

    public required string TerritoryName { get; init; }

    public required TimeSpan Duration { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public IReadOnlyList<ActorRecord> Actors { get; init; } = [];

    public IReadOnlyList<AnalysisResult> Results { get; init; } = [];

    public IReadOnlyList<SessionRuleOpportunity> Opportunities { get; init; } = [];

    public IReadOnlyList<SessionProgressObservation> Progress { get; init; } = [];

    public SessionPullOutcome Outcome { get; init; } = new() { Kind = SessionPullOutcomeKind.Unknown };

    /// <summary>
    /// Optional mapping from pull-local actor identities to stable session participant identities.
    /// Entries are present only when the assembler has enough evidence to resolve them safely.
    /// </summary>
    public IReadOnlyDictionary<ActorId, SessionParticipantKey> ParticipantKeys { get; init; } =
        new Dictionary<ActorId, SessionParticipantKey>();
}

internal sealed record RaidSession
{
    public required RaidSessionId Id { get; init; }

    public required uint TerritoryId { get; init; }

    public required string TerritoryName { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    public required IReadOnlyList<SessionPullAnalysis> Pulls { get; init; }

    public IReadOnlyList<SessionParticipant> Participants { get; init; } = [];
}
