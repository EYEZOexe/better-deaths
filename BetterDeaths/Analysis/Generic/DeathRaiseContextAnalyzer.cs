namespace BetterDeaths.Analysis.Generic;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Index;
using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed class DeathRaiseContextAnalyzer : IAnalyzerModule
{
    public const string AnalyzerId = "generic.death-raise-context";

    private static readonly TimeSpan FatalContextLookback = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RaiseObservationLookahead = TimeSpan.FromSeconds(60);
    private const int MaximumDamageContextEvents = 8;

    public string Id => AnalyzerId;

    public AnalyzerScope Scope => AnalyzerScope.Generic;

    public IReadOnlyCollection<string> Dependencies => Array.Empty<string>();

    public bool Supports(AnalyzerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Events.OfType<DeathEvent>().Count > 0;
    }

    public ValueTask AnalyzeAsync(
        AnalyzerContext context,
        IAnalysisResultSink results,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(results);

        var deaths = context.Events.OfType<DeathEvent>()
            .OrderBy(death => death.Sequence)
            .ToArray();
        for (var index = 0; index < deaths.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var death = deaths[index];
            var nextDeath = FindNextDeathForSameActor(deaths, index, death.TargetActorId);
            AnalyzeDeath(context, death, nextDeath, results);
        }

        return ValueTask.CompletedTask;
    }

    private static void AnalyzeDeath(
        AnalyzerContext context,
        DeathEvent death,
        DeathEvent? nextDeath,
        IAnalysisResultSink results)
    {
        var actorId = death.TargetActorId;
        var actorName = ResolveActorName(context, actorId);
        var start = death.PullTime > FatalContextLookback
            ? death.PullTime - FatalContextLookback
            : TimeSpan.Zero;
        var contextRange = new TimeRange(start, death.PullTime);
        var targetEvents = actorId is { } targetActorId
            ? context.Events.ToActor(targetActorId)
            : Array.Empty<NormalizedEvent>();
        var recentDamage = targetEvents
            .OfType<DamageEvent>()
            .Where(evt => evt.PullTime >= start && evt.PullTime <= death.PullTime)
            .OrderByDescending(evt => evt.Sequence)
            .Take(MaximumDamageContextEvents)
            .OrderBy(evt => evt.Sequence)
            .ToArray();
        var statusContext = actorId is { } statusActorId
            ? GetStatusContext(context, statusActorId, death.PullTime)
            : StatusContext.Empty;
        var raise = actorId is { } raiseTarget
            ? FindRaiseObservation(context, raiseTarget, death, nextDeath)
            : null;

        var actors = actorId is { } deathTarget
            ? new[] { deathTarget }
            : Array.Empty<ActorId>();
        var evidence = new List<AnalysisEvidence>
        {
            new()
            {
                EventIds = [death.Id],
                ActorIds = actors,
                TimeRange = new TimeRange(death.PullTime, death.PullTime),
                Explanation = "Canonical DeathEvent evidence establishes that the death was observed.",
            },
        };

        if (recentDamage.Length > 0)
        {
            evidence.Add(new AnalysisEvidence
            {
                EventIds = recentDamage.Select(evt => evt.Id).ToArray(),
                ActorIds = BuildActorEvidence(recentDamage, actorId),
                TimeRange = new TimeRange(recentDamage[0].PullTime, recentDamage[^1].PullTime),
                Explanation =
                    "Recent damage events are fatal-context candidates only. The canonical damage contract does not " +
                    "contain target post-event HP/shield state, so chronological proximity is not treated as lethal attribution or blame.",
            });
        }

        if (statusContext.Known.Count > 0)
        {
            evidence.Add(new AnalysisEvidence
            {
                EventIds = statusContext.Known
                    .SelectMany(interval => interval.EvidenceEventIds)
                    .Distinct()
                    .ToArray(),
                ActorIds = BuildStatusActors(statusContext.Known, actorId),
                TimeRange = GetIntervalEvidenceRange(statusContext.Known, death.PullTime),
                Explanation =
                    "Status interval evidence supports these statuses being active at the death timestamp. " +
                    "Generic status presence is context, not proof that a status caused the death.",
            });
        }

        if (statusContext.Uncertain.Count > 0)
        {
            evidence.Add(new AnalysisEvidence
            {
                EventIds = statusContext.Uncertain
                    .SelectMany(interval => interval.EvidenceEventIds)
                    .Distinct()
                    .ToArray(),
                ActorIds = BuildStatusActors(statusContext.Uncertain, actorId),
                TimeRange = GetIntervalEvidenceRange(statusContext.Uncertain, death.PullTime),
                Explanation =
                    "These statuses were observed before death but their ending coverage is unknown. They are retained as uncertain context, not assumed active.",
            });
        }

        if (raise is not null)
        {
            evidence.Add(new AnalysisEvidence
            {
                EventIds = [death.Id, raise.Id],
                ActorIds = BuildRaiseActors(actorId, raise.SourceActorId),
                TimeRange = new TimeRange(death.PullTime, raise.PullTime),
                Explanation =
                    "A canonical RaiseEvent was observed after the death. This is downstream raise evidence and does not establish recovery completion time.",
            });
        }

        var deathSummary = recentDamage.Length == 0
            ? "The death was recorded, but no canonical damage event was captured in the 10-second fatal-context window. "
            : $"The death was recorded with {recentDamage.Length} recent damage event(s) in the 10-second fatal-context window. ";
        deathSummary +=
            "Current canonical damage events do not include target post-event HP/shield state, so this analyzer does not label any nearby hit as lethal or as a player mistake.";
        if (statusContext.Known.Count > 0 || statusContext.Uncertain.Count > 0)
        {
            deathSummary +=
                $" Status context at death: {statusContext.Known.Count} evidence-supported active interval(s) and " +
                $"{statusContext.Uncertain.Count} uncertain interval(s).";
        }

        if (raise is not null)
        {
            deathSummary += $" A raise event was observed {(raise.PullTime - death.PullTime).TotalSeconds:F1}s later.";
        }

        results.Add(new AnalysisResult
        {
            Id = StableAnalysisResultIdentity.ForEvent(context.Pull.Id, AnalyzerId, death.Id),
            AnalyzerId = AnalyzerId,
            Severity = AnalysisSeverity.Error,
            Category = AnalysisCategory.Death,
            Title = string.IsNullOrWhiteSpace(actorName) ? "Death context" : $"{actorName}: death context",
            Summary = deathSummary,
            TimeRange = contextRange,
            Actors = actors,
            Evidence = evidence,
            Confidence = Math.Clamp(death.Provenance.Confidence, 0.0f, 1.0f),
            Metrics = BuildDeathMetrics(recentDamage.Length, statusContext, raise, death),
        });

        if (raise is not null)
        {
            AddRaiseResult(context, death, raise, actorId, actorName, results);
        }
    }

    private static void AddRaiseResult(
        AnalyzerContext context,
        DeathEvent death,
        RaiseEvent raise,
        ActorId? actorId,
        string? actorName,
        IAnalysisResultSink results)
    {
        var delay = raise.PullTime - death.PullTime;
        var actors = BuildRaiseActors(actorId, raise.SourceActorId);
        var confidence = Math.Clamp(
            Math.Min(death.Provenance.Confidence, raise.Provenance.Confidence),
            0.0f,
            1.0f);
        var metrics = new Dictionary<string, double>
        {
            ["secondsAfterDeath"] = delay.TotalSeconds,
        };
        if (raise.ActionId is { } actionId)
        {
            metrics["raiseActionId"] = actionId;
        }

        results.Add(new AnalysisResult
        {
            Id = StableAnalysisResultIdentity.ForEvent(context.Pull.Id, AnalyzerId, raise.Id),
            AnalyzerId = AnalyzerId,
            Severity = AnalysisSeverity.Observation,
            Category = AnalysisCategory.Raise,
            Title = string.IsNullOrWhiteSpace(actorName)
                ? $"Raise observed {delay.TotalSeconds:F1}s after death"
                : $"{actorName}: raise observed {delay.TotalSeconds:F1}s after death",
            Summary =
                "A canonical RaiseEvent was observed after the death. This timestamps raise evidence, not confirmed resurrection completion, HP restoration, or the end of recovery downtime.",
            TimeRange = new TimeRange(death.PullTime, raise.PullTime),
            Actors = actors,
            Evidence =
            [
                new AnalysisEvidence
                {
                    EventIds = [death.Id, raise.Id],
                    ActorIds = actors,
                    TimeRange = new TimeRange(death.PullTime, raise.PullTime),
                    Explanation = "The death and subsequent raise event bound this observed recovery-action interval.",
                },
            ],
            Confidence = confidence,
            Metrics = metrics,
        });
    }

    private static StatusContext GetStatusContext(
        AnalyzerContext context,
        ActorId actorId,
        TimeSpan deathTime)
    {
        var statusIds = context.Events.ToActor(actorId)
            .Select(evt => evt switch
            {
                StatusApplyEvent apply => (uint?)apply.StatusId,
                StatusRemoveEvent remove => remove.StatusId,
                _ => null,
            })
            .Where(statusId => statusId.HasValue)
            .Select(statusId => statusId!.Value)
            .Distinct()
            .OrderBy(statusId => statusId)
            .ToArray();
        var known = new List<StatusInterval>();
        var uncertain = new List<StatusInterval>();
        foreach (var statusId in statusIds)
        {
            foreach (var interval in context.Statuses.ForActorStatus(actorId, statusId))
            {
                if (deathTime < interval.Start || deathTime >= interval.End)
                {
                    continue;
                }

                if (interval.CoverageKnownThroughEnd)
                {
                    known.Add(interval);
                }
                else
                {
                    uncertain.Add(interval);
                }
            }
        }

        return new StatusContext(known, uncertain);
    }

    private static RaiseEvent? FindRaiseObservation(
        AnalyzerContext context,
        ActorId actorId,
        DeathEvent death,
        DeathEvent? nextDeath)
    {
        var latest = death.PullTime + RaiseObservationLookahead;
        if (nextDeath is not null && nextDeath.PullTime < latest)
        {
            latest = nextDeath.PullTime;
        }

        return context.Events.ToActor(actorId)
            .OfType<RaiseEvent>()
            .Where(raise => raise.PullTime >= death.PullTime && raise.PullTime < latest)
            .OrderBy(raise => raise.Sequence)
            .FirstOrDefault();
    }

    private static DeathEvent? FindNextDeathForSameActor(
        IReadOnlyList<DeathEvent> deaths,
        int currentIndex,
        ActorId? actorId)
    {
        if (actorId is null)
        {
            return null;
        }

        for (var index = currentIndex + 1; index < deaths.Count; index++)
        {
            if (deaths[index].TargetActorId == actorId)
            {
                return deaths[index];
            }
        }

        return null;
    }

    private static string? ResolveActorName(AnalyzerContext context, ActorId? actorId)
    {
        return actorId is { } id && context.Actors.TryGet(id, out var actor)
            ? actor?.Name
            : null;
    }

    private static IReadOnlyList<ActorId> BuildActorEvidence(
        IReadOnlyList<DamageEvent> events,
        ActorId? targetActorId)
    {
        return events
            .SelectMany(evt => new ActorId?[] { evt.SourceActorId, evt.TargetActorId })
            .Append(targetActorId)
            .Where(actorId => actorId.HasValue)
            .Select(actorId => actorId!.Value)
            .Distinct()
            .OrderBy(actorId => actorId.Value)
            .ToArray();
    }

    private static IReadOnlyList<ActorId> BuildStatusActors(
        IReadOnlyList<StatusInterval> intervals,
        ActorId? targetActorId)
    {
        return intervals
            .Select(interval => interval.Key.SourceActorId)
            .Append(targetActorId)
            .Where(actorId => actorId.HasValue)
            .Select(actorId => actorId!.Value)
            .Distinct()
            .OrderBy(actorId => actorId.Value)
            .ToArray();
    }

    private static IReadOnlyList<ActorId> BuildRaiseActors(ActorId? targetActorId, ActorId? sourceActorId)
    {
        return new[] { targetActorId, sourceActorId }
            .Where(actorId => actorId.HasValue)
            .Select(actorId => actorId!.Value)
            .Distinct()
            .OrderBy(actorId => actorId.Value)
            .ToArray();
    }

    private static TimeRange GetIntervalEvidenceRange(
        IReadOnlyList<StatusInterval> intervals,
        TimeSpan deathTime)
    {
        var start = intervals.Min(interval => interval.Start);
        var end = intervals.Max(interval => interval.End);
        if (end < deathTime)
        {
            end = deathTime;
        }

        return new TimeRange(start, end);
    }

    private static Dictionary<string, double> BuildDeathMetrics(
        int recentDamageCount,
        StatusContext statusContext,
        RaiseEvent? raise,
        DeathEvent death)
    {
        var metrics = new Dictionary<string, double>
        {
            ["pullTimeSeconds"] = death.PullTime.TotalSeconds,
            ["recentDamageEvents"] = recentDamageCount,
            ["knownStatusIntervalsAtDeath"] = statusContext.Known.Count,
            ["uncertainStatusIntervalsAtDeath"] = statusContext.Uncertain.Count,
            ["lethalAttributionAvailable"] = 0,
            ["raiseObserved"] = raise is null ? 0 : 1,
        };
        if (raise is not null)
        {
            metrics["secondsToRaiseObservation"] = (raise.PullTime - death.PullTime).TotalSeconds;
        }

        return metrics;
    }

    private sealed record StatusContext(
        IReadOnlyList<StatusInterval> Known,
        IReadOnlyList<StatusInterval> Uncertain)
    {
        public static StatusContext Empty { get; } = new(
            Array.Empty<StatusInterval>(),
            Array.Empty<StatusInterval>());
    }
}
