namespace BetterDeaths.Windows.Analyzer;

using BetterDeaths.Analysis.Encounters.DancingMadUltimate;
using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Sessions;
using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

internal sealed record AnalyzerSessionRequest
{
    public required uint TerritoryId { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public int Limit { get; init; } = 500;

    public void Validate()
    {
        if (TerritoryId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TerritoryId));
        }

        if (Limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Limit));
        }

        if (From is { } from && To is { } to && to < from)
        {
            throw new ArgumentException("Session request end cannot precede start.", nameof(To));
        }
    }
}

internal enum AnalyzerSessionDiagnosticKind
{
    MissingPull,
    PullLoadFailure,
    TerritoryMismatch,
    AnalyzerFailure,
    PullEnrichmentFailure,
}

internal sealed record AnalyzerSessionDiagnostic
{
    public required AnalyzerSessionDiagnosticKind Kind { get; init; }

    public required PullId PullId { get; init; }

    public string? AnalyzerId { get; init; }

    public required string Message { get; init; }
}

internal sealed record AnalyzerSessionLoadProgress
{
    public required int SelectedPullCount { get; init; }

    public required int ProcessedPullCount { get; init; }

    public required int LoadedPullCount { get; init; }

    public required int DiagnosticCount { get; init; }
}

internal sealed record AnalyzerSessionLoaded
{
    public required RaidSession Session { get; init; }

    public required SessionIntelligenceResult Analysis { get; init; }

    public required IReadOnlyList<AnalyzerSessionDiagnostic> Diagnostics { get; init; }

    public required int SelectedPullCount { get; init; }
}

internal sealed record AnalyzerSessionPullEnrichment
{
    public IReadOnlyList<SessionRuleOpportunity> Opportunities { get; init; } = [];

    public IReadOnlyList<SessionProgressObservation> Progress { get; init; } = [];

    public SessionPullOutcome Outcome { get; init; } = new() { Kind = SessionPullOutcomeKind.Unknown };

    public IReadOnlyDictionary<ActorId, SessionParticipantKey> ParticipantKeys { get; init; } =
        new Dictionary<ActorId, SessionParticipantKey>();

    public IReadOnlyList<SessionParticipant> Participants { get; init; } = [];
}

internal interface IAnalyzerSessionPullEnricher
{
    AnalyzerSessionPullEnrichment Enrich(RecordedPull pull, AnalyzerRunResult run);
}

internal sealed class DefaultAnalyzerSessionPullEnricher : IAnalyzerSessionPullEnricher
{
    private static readonly SessionFindingKey ForsakenIncompatibleKey = new(
        ForsakenOpeningAssignmentAnalyzer.AnalyzerId,
        ForsakenOpeningAssignmentAnalyzer.IncompatibleAssignmentRuleKey);

    private static readonly IReadOnlySet<string> ForsakenCompleteResultKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        ForsakenOpeningAssignmentAnalyzer.ResolvedAssignmentRuleKey,
        ForsakenOpeningAssignmentAnalyzer.AmbiguousAssignmentRuleKey,
        ForsakenOpeningAssignmentAnalyzer.IncompatibleAssignmentRuleKey,
    };

    public AnalyzerSessionPullEnrichment Enrich(RecordedPull pull, AnalyzerRunResult run)
    {
        ArgumentNullException.ThrowIfNull(pull);
        ArgumentNullException.ThrowIfNull(run);

        if (pull.Metadata.TerritoryId != ForsakenDefinition.TerritoryId)
        {
            return new AnalyzerSessionPullEnrichment();
        }

        var forsakenResults = run.Results
            .Where(result => string.Equals(result.AnalyzerId, ForsakenOpeningAssignmentAnalyzer.AnalyzerId, StringComparison.Ordinal))
            .Where(result => result.RuleKey is not null && ForsakenCompleteResultKeys.Contains(result.RuleKey))
            .ToArray();
        var progress = forsakenResults.Length == 0
            ? Array.Empty<SessionProgressObservation>()
            :
            [
                new SessionProgressObservation
                {
                    PhaseKey = ForsakenDefinition.PhaseKey,
                    PhaseOrder = 2,
                    ReachedAt = forsakenResults
                        .Where(result => result.TimeRange is not null)
                        .Select(result => result.TimeRange!.Value.Start)
                        .DefaultIfEmpty()
                        .Min(),
                },
            ];

        var opportunityState = HasExactCompleteForsakenEvidence(pull, forsakenResults)
            ? SessionOpportunityState.Evaluable
            : SessionOpportunityState.Unknown;

        return new AnalyzerSessionPullEnrichment
        {
            Opportunities =
            [
                new SessionRuleOpportunity
                {
                    Key = ForsakenIncompatibleKey,
                    State = opportunityState,
                },
            ],
            Progress = progress,
        };
    }

    private static bool HasExactCompleteForsakenEvidence(
        RecordedPull pull,
        IReadOnlyList<AnalysisResult> forsakenResults)
    {
        if (pull.Provenance.Fidelity != CaptureFidelity.Exact || forsakenResults.Count == 0)
        {
            return false;
        }

        var evidenceIds = forsakenResults
            .SelectMany(result => result.Evidence)
            .SelectMany(evidence => evidence.EventIds)
            .Distinct()
            .ToHashSet();
        if (evidenceIds.Count < 8)
        {
            return false;
        }

        var evidenceEvents = pull.Events.Where(evt => evidenceIds.Contains(evt.Id)).ToArray();
        return evidenceEvents.Length == evidenceIds.Count &&
               evidenceEvents.All(evt => evt.Provenance.Fidelity == CaptureFidelity.Exact);
    }
}

internal sealed class AnalyzerSessionDataController
{
    private readonly IPullStore pullStore;
    private readonly AnalyzerEngine analyzerEngine;
    private readonly IAnalyzerSessionPullEnricher pullEnricher;
    private long generation;

    public AnalyzerSessionDataController(
        IPullStore pullStore,
        AnalyzerEngine analyzerEngine,
        IAnalyzerSessionPullEnricher? pullEnricher = null)
    {
        ArgumentNullException.ThrowIfNull(pullStore);
        ArgumentNullException.ThrowIfNull(analyzerEngine);
        this.pullStore = pullStore;
        this.analyzerEngine = analyzerEngine;
        this.pullEnricher = pullEnricher ?? new DefaultAnalyzerSessionPullEnricher();
    }

    public static AnalyzerSessionDataController CreateDefault(IPullStore pullStore)
    {
        ArgumentNullException.ThrowIfNull(pullStore);
        return new AnalyzerSessionDataController(pullStore, AnalyzerWorkspaceEngineComposition.CreateDefault());
    }

    public void InvalidatePendingLoad()
    {
        Interlocked.Increment(ref generation);
    }

    public async Task<AnalyzerSessionLoaded?> LoadAsync(
        AnalyzerSessionRequest request,
        IProgress<AnalyzerSessionLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var loadGeneration = Interlocked.Increment(ref generation);

        var summaries = await pullStore.QueryAsync(
            new PullQuery
            {
                TerritoryId = request.TerritoryId,
                Limit = request.Limit,
            },
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrent(loadGeneration))
        {
            return null;
        }

        var selected = summaries
            .Where(summary => summary.TerritoryId == request.TerritoryId)
            .Where(summary => MatchesRequestedTime(summary, request))
            .OrderBy(summary => summary.StartedAt.HasValue ? 0 : 1)
            .ThenBy(summary => summary.StartedAt ?? DateTimeOffset.MaxValue)
            .ThenBy(summary => summary.Id.Value)
            .ToArray();

        var diagnostics = new List<AnalyzerSessionDiagnostic>();
        var analyzedPulls = new List<SessionPullAnalysis>(selected.Length);
        var participants = new Dictionary<SessionParticipantKey, SessionParticipant>();
        var processed = 0;

        ReportProgress(progress, selected.Length, processed, analyzedPulls.Count, diagnostics.Count);
        foreach (var summary in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(loadGeneration))
            {
                return null;
            }

            RecordedPull? pull;
            try
            {
                pull = await pullStore.LoadAsync(summary.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                diagnostics.Add(Diagnostic(
                    AnalyzerSessionDiagnosticKind.PullLoadFailure,
                    summary.Id,
                    $"{exception.GetType().Name}: {exception.Message}"));
                processed++;
                ReportProgress(progress, selected.Length, processed, analyzedPulls.Count, diagnostics.Count);
                continue;
            }

            if (pull is null)
            {
                diagnostics.Add(Diagnostic(
                    AnalyzerSessionDiagnosticKind.MissingPull,
                    summary.Id,
                    "The pull summary exists but the full canonical pull could not be loaded."));
                processed++;
                ReportProgress(progress, selected.Length, processed, analyzedPulls.Count, diagnostics.Count);
                continue;
            }

            if (pull.Metadata.TerritoryId != request.TerritoryId)
            {
                diagnostics.Add(Diagnostic(
                    AnalyzerSessionDiagnosticKind.TerritoryMismatch,
                    pull.Id,
                    $"Loaded territory {pull.Metadata.TerritoryId} does not match requested territory {request.TerritoryId}."));
                processed++;
                ReportProgress(progress, selected.Length, processed, analyzedPulls.Count, diagnostics.Count);
                continue;
            }

            AnalyzerRunResult run;
            try
            {
                run = await analyzerEngine.AnalyzeAsync(pull, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                diagnostics.Add(Diagnostic(
                    AnalyzerSessionDiagnosticKind.AnalyzerFailure,
                    pull.Id,
                    $"Analyzer engine failed: {exception.GetType().Name}: {exception.Message}"));
                processed++;
                ReportProgress(progress, selected.Length, processed, analyzedPulls.Count, diagnostics.Count);
                continue;
            }

            foreach (var failure in run.Failures)
            {
                diagnostics.Add(new AnalyzerSessionDiagnostic
                {
                    Kind = AnalyzerSessionDiagnosticKind.AnalyzerFailure,
                    PullId = pull.Id,
                    AnalyzerId = failure.AnalyzerId,
                    Message = $"{failure.ExceptionType}: {failure.Message}",
                });
            }

            AnalyzerSessionPullEnrichment enrichment;
            try
            {
                enrichment = pullEnricher.Enrich(pull, run);
            }
            catch (Exception exception)
            {
                diagnostics.Add(Diagnostic(
                    AnalyzerSessionDiagnosticKind.PullEnrichmentFailure,
                    pull.Id,
                    $"{exception.GetType().Name}: {exception.Message}"));
                enrichment = new AnalyzerSessionPullEnrichment();
            }

            analyzedPulls.Add(new SessionPullAnalysis
            {
                PullId = pull.Id,
                TerritoryId = pull.Metadata.TerritoryId,
                TerritoryName = pull.Metadata.TerritoryName,
                Duration = pull.Metadata.Duration,
                StartedAt = pull.Metadata.StartedAt,
                Actors = pull.Actors,
                Results = run.Results,
                Opportunities = enrichment.Opportunities,
                Progress = enrichment.Progress,
                Outcome = enrichment.Outcome,
                ParticipantKeys = enrichment.ParticipantKeys,
            });
            MergeParticipants(participants, enrichment.Participants);

            processed++;
            ReportProgress(progress, selected.Length, processed, analyzedPulls.Count, diagnostics.Count);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrent(loadGeneration))
        {
            return null;
        }

        var territoryName = analyzedPulls.FirstOrDefault()?.TerritoryName ??
                            selected.FirstOrDefault()?.TerritoryName ??
                            $"Territory {request.TerritoryId}";
        var session = new RaidSession
        {
            Id = CreateSessionId(request.TerritoryId, analyzedPulls.Select(pull => pull.PullId)),
            TerritoryId = request.TerritoryId,
            TerritoryName = territoryName,
            StartedAt = analyzedPulls.Where(pull => pull.StartedAt is not null).MinBy(pull => pull.StartedAt)?.StartedAt,
            EndedAt = ResolveSessionEnd(analyzedPulls),
            Pulls = analyzedPulls.ToArray(),
            Participants = participants.Values.OrderBy(participant => participant.Key.Value, StringComparer.Ordinal).ToArray(),
        };

        return new AnalyzerSessionLoaded
        {
            Session = session,
            Analysis = SessionIntelligenceAnalyzer.Analyze(session),
            Diagnostics = diagnostics.ToArray(),
            SelectedPullCount = selected.Length,
        };
    }

    private bool IsCurrent(long loadGeneration)
    {
        return loadGeneration == Volatile.Read(ref generation);
    }

    private static bool MatchesRequestedTime(PullSummary summary, AnalyzerSessionRequest request)
    {
        if (request.From is null && request.To is null)
        {
            return true;
        }

        if (summary.StartedAt is not { } startedAt)
        {
            return false;
        }

        return (request.From is null || startedAt >= request.From.Value) &&
               (request.To is null || startedAt <= request.To.Value);
    }

    private static DateTimeOffset? ResolveSessionEnd(IReadOnlyList<SessionPullAnalysis> pulls)
    {
        var knownEnds = pulls
            .Where(pull => pull.StartedAt is not null)
            .Select(pull => pull.StartedAt!.Value + pull.Duration)
            .ToArray();
        return knownEnds.Length == 0 ? null : knownEnds.Max();
    }

    private static RaidSessionId CreateSessionId(uint territoryId, IEnumerable<PullId> pullIds)
    {
        var orderedIds = pullIds.Select(id => id.Value.ToString("N")).Order(StringComparer.Ordinal);
        var identity = $"{territoryId}|{string.Join('|', orderedIds)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new RaidSessionId(new Guid(hash.AsSpan(0, 16)));
    }

    private static void MergeParticipants(
        IDictionary<SessionParticipantKey, SessionParticipant> participants,
        IReadOnlyList<SessionParticipant> additions)
    {
        foreach (var participant in additions)
        {
            ArgumentNullException.ThrowIfNull(participant);
            if (participants.TryGetValue(participant.Key, out var existing))
            {
                if (existing != participant)
                {
                    throw new InvalidOperationException(
                        $"Session participant key '{participant.Key.Value}' resolved to conflicting metadata.");
                }

                continue;
            }

            participants.Add(participant.Key, participant);
        }
    }

    private static AnalyzerSessionDiagnostic Diagnostic(
        AnalyzerSessionDiagnosticKind kind,
        PullId pullId,
        string message)
    {
        return new AnalyzerSessionDiagnostic
        {
            Kind = kind,
            PullId = pullId,
            Message = message,
        };
    }

    private static void ReportProgress(
        IProgress<AnalyzerSessionLoadProgress>? progress,
        int selected,
        int processed,
        int loaded,
        int diagnostics)
    {
        progress?.Report(new AnalyzerSessionLoadProgress
        {
            SelectedPullCount = selected,
            ProcessedPullCount = processed,
            LoadedPullCount = loaded,
            DiagnosticCount = diagnostics,
        });
    }
}
