namespace BetterDeaths.Analysis.Generic;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Index;
using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed class TargetabilityAwareUptimeAnalyzer : IAnalyzerModule
{
    public const string AnalyzerId = "generic.targetability-uptime";

    private static readonly TimeSpan MinimumObservedGap = TimeSpan.FromSeconds(5);

    public string Id => AnalyzerId;

    public AnalyzerScope Scope => AnalyzerScope.Generic;

    public IReadOnlyCollection<string> Dependencies => Array.Empty<string>();

    public bool Supports(AnalyzerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Pull.Actors.Any(actor => actor.Kind == ActorKind.Player) &&
            context.Pull.Actors.Any(actor => actor.Kind == ActorKind.Enemy) &&
            context.Events.OfType<TargetabilityEvent>().Count > 0;
    }

    public ValueTask AnalyzeAsync(
        AnalyzerContext context,
        IAnalysisResultSink results,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(results);

        var primaryTarget = SelectPrimaryEnemy(context);
        if (primaryTarget is null)
        {
            return ValueTask.CompletedTask;
        }

        var targetableIntervals = context.Targetability
            .ForActor(primaryTarget.Id)
            .Where(interval => interval.IsTargetable && interval.Duration > TimeSpan.Zero)
            .ToArray();
        if (targetableIntervals.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        var targetabilityEvents = context.Events
            .OfType<TargetabilityEvent>()
            .ToDictionary(evt => evt.Id);
        var players = context.Pull.Actors
            .Where(actor => actor.Kind == ActorKind.Player)
            .OrderBy(actor => actor.Id.Value)
            .ToArray();

        foreach (var player in players)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Events.ToActor(player.Id).Any(evt => evt is DeathEvent))
            {
                // M5-D owns death/raise downtime. Generic uptime must not turn death downtime
                // into a normal execution inactivity finding.
                continue;
            }

            var activity = context.Events
                .FromActor(player.Id)
                .Where(IsPlayerActivity)
                .OrderBy(evt => evt.PullTime)
                .ThenBy(evt => evt.Sequence)
                .ToArray();
            if (activity.Length < 2)
            {
                continue;
            }

            for (var index = 1; index < activity.Length; index++)
            {
                var previous = activity[index - 1];
                var next = activity[index];
                if (next.PullTime <= previous.PullTime)
                {
                    continue;
                }

                foreach (var targetableInterval in targetableIntervals)
                {
                    var gapStart = previous.PullTime > targetableInterval.Start
                        ? previous.PullTime
                        : targetableInterval.Start;
                    var gapEnd = next.PullTime < targetableInterval.End
                        ? next.PullTime
                        : targetableInterval.End;
                    var gapDuration = gapEnd - gapStart;
                    if (gapDuration < MinimumObservedGap)
                    {
                        continue;
                    }

                    var gapRange = new TimeRange(gapStart, gapEnd);
                    var evidenceIds = targetableInterval.EvidenceEventIds
                        .Append(previous.Id)
                        .Append(next.Id)
                        .Distinct()
                        .ToArray();
                    var confidence = GetConfidence(
                        targetableInterval,
                        targetabilityEvents,
                        previous,
                        next);
                    results.Add(new AnalysisResult
                    {
                        Id = StableAnalysisResultIdentity.ForActorWindow(
                            context.Pull.Id,
                            AnalyzerId,
                            player.Id,
                            gapRange,
                            $"target:{primaryTarget.Id.Value}"),
                        AnalyzerId = AnalyzerId,
                        Severity = AnalysisSeverity.Observation,
                        Category = AnalysisCategory.Uptime,
                        Title = $"{player.Name}: {gapDuration.TotalSeconds:F1}s observed action gap",
                        Summary =
                            $"No canonical player action was observed for {gapDuration.TotalSeconds:F1}s while " +
                            $"{primaryTarget.Name} was evidence-supported as targetable. This is an activity observation, " +
                            "not a job-rotation or blame verdict.",
                        TimeRange = gapRange,
                        Actors = [player.Id, primaryTarget.Id],
                        Evidence =
                        [
                            new AnalysisEvidence
                            {
                                EventIds = evidenceIds,
                                ActorIds = [player.Id, primaryTarget.Id],
                                TimeRange = gapRange,
                                Explanation =
                                    "The surrounding player activity events bound the gap; targetability transition evidence " +
                                    "supports that this portion of the gap occurred during a targetable window.",
                            },
                        ],
                        Confidence = confidence,
                        Metrics = new Dictionary<string, double>
                        {
                            ["observedGapSeconds"] = gapDuration.TotalSeconds,
                            ["targetActorId"] = primaryTarget.Id.Value,
                        },
                    });
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    private static ActorRecord? SelectPrimaryEnemy(AnalyzerContext context)
    {
        var enemies = context.Pull.Actors
            .Where(actor => actor.Kind == ActorKind.Enemy)
            .OrderBy(actor => actor.Id.Value)
            .ToArray();
        if (enemies.Length == 0)
        {
            return null;
        }

        var enemyIds = enemies.Select(actor => actor.Id).ToHashSet();
        var playerIds = context.Pull.Actors
            .Where(actor => actor.Kind == ActorKind.Player)
            .Select(actor => actor.Id)
            .ToHashSet();
        var interactionCounts = enemies.ToDictionary(actor => actor.Id, _ => 0);

        foreach (var evt in context.Events.All)
        {
            if (evt.SourceActorId is not { } sourceActorId ||
                evt.TargetActorId is not { } targetActorId ||
                !playerIds.Contains(sourceActorId) ||
                !enemyIds.Contains(targetActorId) ||
                !IsOffensiveInteraction(evt))
            {
                continue;
            }

            interactionCounts[targetActorId]++;
        }

        return enemies
            .OrderByDescending(actor => interactionCounts[actor.Id])
            .ThenByDescending(actor => context.Targetability.GetCoverage(actor.Id).TargetableDuration)
            .ThenBy(actor => actor.Id.Value)
            .FirstOrDefault();
    }

    private static bool IsPlayerActivity(NormalizedEvent evt)
    {
        return evt is ActionUseEvent or CastStartEvent or DamageEvent or HealEvent or RaiseEvent;
    }

    private static bool IsOffensiveInteraction(NormalizedEvent evt)
    {
        return evt is ActionUseEvent or CastStartEvent or CastEndEvent or DamageEvent;
    }

    private static float GetConfidence(
        TargetabilityInterval interval,
        IReadOnlyDictionary<EventId, TargetabilityEvent> targetabilityEvents,
        NormalizedEvent previous,
        NormalizedEvent next)
    {
        var confidence = Math.Min(previous.Provenance.Confidence, next.Provenance.Confidence);
        foreach (var eventId in interval.EvidenceEventIds)
        {
            if (targetabilityEvents.TryGetValue(eventId, out var targetability))
            {
                confidence = Math.Min(confidence, targetability.Provenance.Confidence);
            }
        }

        return Math.Clamp(confidence, 0.0f, 1.0f);
    }
}
