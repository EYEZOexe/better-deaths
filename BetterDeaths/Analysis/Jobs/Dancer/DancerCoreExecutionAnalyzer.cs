namespace BetterDeaths.Analysis.Jobs.Dancer;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Index;
using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed class DancerCoreExecutionAnalyzer : IAnalyzerModule
{
    public const string AnalyzerId = "job.dnc.core";

    private static readonly TimeSpan DanceSequenceLimit = TimeSpan.FromSeconds(15);

    private static readonly IReadOnlyDictionary<uint, DanceFinishDefinition> StandardFinishes =
        new Dictionary<uint, DanceFinishDefinition>
        {
            [ActionId(DancerJobDefinition.StandardFinish)] = new("Standard Finish", 0),
            [ActionId(DancerJobDefinition.SingleStandardFinish)] = new("Single Standard Finish", 1),
            [ActionId(DancerJobDefinition.DoubleStandardFinish)] = new("Double Standard Finish", 2),
        };

    private static readonly IReadOnlyDictionary<uint, DanceFinishDefinition> TechnicalFinishes =
        new Dictionary<uint, DanceFinishDefinition>
        {
            [ActionId(DancerJobDefinition.TechnicalFinish)] = new("Technical Finish", 0),
            [ActionId(DancerJobDefinition.SingleTechnicalFinish)] = new("Single Technical Finish", 1),
            [ActionId(DancerJobDefinition.DoubleTechnicalFinish)] = new("Double Technical Finish", 2),
            [ActionId(DancerJobDefinition.TripleTechnicalFinish)] = new("Triple Technical Finish", 3),
            [ActionId(DancerJobDefinition.QuadrupleTechnicalFinish)] = new("Quadruple Technical Finish", 4),
        };

    private static readonly IReadOnlySet<uint> DanceStepActions = new HashSet<uint>
    {
        ActionId(DancerJobDefinition.Emboite),
        ActionId(DancerJobDefinition.Entrechat),
        ActionId(DancerJobDefinition.Jete),
        ActionId(DancerJobDefinition.Pirouette),
    };

    private static readonly IReadOnlyList<ProcConsumptionDefinition> ProcDefinitions =
    [
        Proc(DancerJobDefinition.SilkenSymmetry, "Silken Symmetry", DancerJobDefinition.ReverseCascade, DancerJobDefinition.RisingWindmill),
        Proc(DancerJobDefinition.SilkenFlow, "Silken Flow", DancerJobDefinition.Fountainfall, DancerJobDefinition.Bloodshower),
        Proc(DancerJobDefinition.FlourishingSymmetry, "Flourishing Symmetry", DancerJobDefinition.ReverseCascade, DancerJobDefinition.RisingWindmill),
        Proc(DancerJobDefinition.FlourishingFlow, "Flourishing Flow", DancerJobDefinition.Fountainfall, DancerJobDefinition.Bloodshower),
        Proc(DancerJobDefinition.ThreefoldFanDance, "Threefold Fan Dance", DancerJobDefinition.FanDanceIII),
        Proc(DancerJobDefinition.FourfoldFanDance, "Fourfold Fan Dance", DancerJobDefinition.FanDanceIV),
        Proc(DancerJobDefinition.FinishingMoveReady, "Finishing Move Ready", DancerJobDefinition.FinishingMove),
        Proc(DancerJobDefinition.LastDanceReady, "Last Dance Ready", DancerJobDefinition.LastDance),
        Proc(DancerJobDefinition.FlourishingStarfall, "Flourishing Starfall", DancerJobDefinition.StarfallDance),
        Proc(DancerJobDefinition.DanceOfTheDawnReady, "Dance of the Dawn Ready", DancerJobDefinition.DanceOfTheDawn),
    ];

    public string Id => AnalyzerId;

    public AnalyzerScope Scope => AnalyzerScope.Job;

    public IReadOnlyCollection<string> Dependencies => Array.Empty<string>();

    public bool Supports(AnalyzerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Pull.Actors.Any(IsDancer);
    }

    public ValueTask AnalyzeAsync(
        AnalyzerContext context,
        IAnalysisResultSink results,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(results);

        var actionUses = context.Events.OfType<ActionUseEvent>();
        foreach (var dancer in context.Pull.Actors.Where(IsDancer).OrderBy(actor => actor.Id.Value))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var actorActions = actionUses
                .Where(evt => evt.SourceActorId == dancer.Id)
                .OrderBy(evt => evt.Sequence)
                .ToArray();

            AnalyzeDanceFinishes(context, dancer, actorActions, results);
            AnalyzeExpiredProcs(context, dancer, actorActions, results);
            AnalyzePartnerEvidence(context, dancer, results);
        }

        return ValueTask.CompletedTask;
    }

    private static void AnalyzeDanceFinishes(
        AnalyzerContext context,
        ActorRecord dancer,
        IReadOnlyList<ActionUseEvent> actions,
        IAnalysisResultSink results)
    {
        for (var index = 0; index < actions.Count; index++)
        {
            var start = actions[index];
            if (start.ActionId == ActionId(DancerJobDefinition.StandardStep))
            {
                AnalyzeDanceSequence(
                    context,
                    dancer,
                    actions,
                    index,
                    start,
                    "Standard Step",
                    requiredSteps: 2,
                    StandardFinishes,
                    results);
            }
            else if (start.ActionId == ActionId(DancerJobDefinition.TechnicalStep))
            {
                AnalyzeDanceSequence(
                    context,
                    dancer,
                    actions,
                    index,
                    start,
                    "Technical Step",
                    requiredSteps: 4,
                    TechnicalFinishes,
                    results);
            }
        }
    }

    private static void AnalyzeDanceSequence(
        AnalyzerContext context,
        ActorRecord dancer,
        IReadOnlyList<ActionUseEvent> actions,
        int startIndex,
        ActionUseEvent start,
        string danceName,
        int requiredSteps,
        IReadOnlyDictionary<uint, DanceFinishDefinition> finishes,
        IAnalysisResultSink results)
    {
        ActionUseEvent? finish = null;
        DanceFinishDefinition? finishDefinition = null;
        var observedStepEvents = new List<ActionUseEvent>();

        for (var index = startIndex + 1; index < actions.Count; index++)
        {
            var candidate = actions[index];
            if (candidate.PullTime - start.PullTime > DanceSequenceLimit)
            {
                break;
            }

            if (candidate.ActionId == ActionId(DancerJobDefinition.StandardStep) ||
                candidate.ActionId == ActionId(DancerJobDefinition.TechnicalStep))
            {
                break;
            }

            if (DanceStepActions.Contains(candidate.ActionId))
            {
                observedStepEvents.Add(candidate);
                continue;
            }

            if (finishes.TryGetValue(candidate.ActionId, out var matchedFinish))
            {
                finish = candidate;
                finishDefinition = matchedFinish;
                break;
            }
        }

        // Absence of a finish is deliberately not treated as a mistake. The pull may end or the
        // source may omit the decisive event. An explicit under-stepped finish is deterministic.
        if (finish is null || finishDefinition is null || finishDefinition.CompletedSteps >= requiredSteps)
        {
            return;
        }

        var evidenceEvents = new List<NormalizedEvent> { start };
        evidenceEvents.AddRange(observedStepEvents);
        evidenceEvents.Add(finish);
        var range = new TimeRange(start.PullTime, finish.PullTime);
        var observedStepCount = observedStepEvents.Count;

        results.Add(new AnalysisResult
        {
            Id = StableAnalysisResultIdentity.ForActorWindow(
                context.Pull.Id,
                AnalyzerId,
                dancer.Id,
                range,
                $"dance-understep:{start.Id.Value}:{finish.Id.Value}"),
            AnalyzerId = AnalyzerId,
            Severity = AnalysisSeverity.Warning,
            Category = AnalysisCategory.Job,
            Title = $"{dancer.Name}: incomplete {danceName}",
            Summary =
                $"{danceName} ended with {finishDefinition.Name}, whose canonical action identity represents " +
                $"{finishDefinition.CompletedSteps} completed dance step(s) instead of the full {requiredSteps}. " +
                $"The event stream also contains {observedStepCount} explicit dance-step action(s) in this sequence. " +
                "This finding uses the explicit finish variant; it does not infer a missing finish from silence.",
            TimeRange = range,
            Actors = [dancer.Id],
            Evidence =
            [
                new AnalysisEvidence
                {
                    EventIds = evidenceEvents.Select(evt => evt.Id).ToArray(),
                    ActorIds = [dancer.Id],
                    TimeRange = range,
                    Explanation =
                        "The Dancer action stream contains an explicit dance start and an explicit under-stepped finish variant within the bounded dance window.",
                },
            ],
            Confidence = EvidenceConfidence(evidenceEvents),
            Metrics = new Dictionary<string, double>
            {
                ["requiredDanceSteps"] = requiredSteps,
                ["finishVariantSteps"] = finishDefinition.CompletedSteps,
                ["observedStepActionCount"] = observedStepCount,
                ["finishActionId"] = finish.ActionId,
            },
        });
    }

    private static void AnalyzeExpiredProcs(
        AnalyzerContext context,
        ActorRecord dancer,
        IReadOnlyList<ActionUseEvent> actions,
        IAnalysisResultSink results)
    {
        foreach (var definition in ProcDefinitions)
        {
            foreach (var interval in context.Statuses.ForActorStatus(dancer.Id, definition.StatusId))
            {
                if (interval.EndReason != StatusIntervalEndReason.DurationExpired ||
                    !interval.CoverageKnownThroughEnd ||
                    !CanUseAbsenceAsEvidence(context, interval))
                {
                    continue;
                }

                var consumed = actions.Any(action =>
                    action.PullTime >= interval.Start &&
                    action.PullTime <= interval.End &&
                    definition.ConsumerActionIds.Contains(action.ActionId));
                if (consumed)
                {
                    continue;
                }

                var evidenceEvents = ResolveEvidenceEvents(context, interval.EvidenceEventIds);
                if (evidenceEvents.Count == 0)
                {
                    continue;
                }

                var range = new TimeRange(interval.Start, interval.End);
                results.Add(new AnalysisResult
                {
                    Id = StableAnalysisResultIdentity.ForActorWindow(
                        context.Pull.Id,
                        AnalyzerId,
                        dancer.Id,
                        range,
                        $"proc-expired:{definition.StatusId}:{interval.Start.Ticks}"),
                    AnalyzerId = AnalyzerId,
                    Severity = AnalysisSeverity.Warning,
                    Category = AnalysisCategory.Job,
                    Title = $"{dancer.Name}: {definition.Name} expired unused",
                    Summary =
                        $"The exact canonical {definition.Name} status interval reached its known duration expiry with no matching Dancer consumer action observed during the interval. " +
                        "This warning is emitted only for exact pull/status evidence; sampled or unknown-ending intervals are not treated as proof of an unused proc.",
                    TimeRange = range,
                    Actors = [dancer.Id],
                    Evidence =
                    [
                        new AnalysisEvidence
                        {
                            EventIds = interval.EvidenceEventIds,
                            ActorIds = [dancer.Id],
                            TimeRange = range,
                            Explanation =
                                "The status application provides a known expiry and exact source fidelity. No configured consumer ActionUseEvent occurs before that expiry.",
                        },
                    ],
                    Confidence = EvidenceConfidence(evidenceEvents),
                    Metrics = new Dictionary<string, double>
                    {
                        ["statusId"] = definition.StatusId,
                        ["knownExpiry"] = 1,
                        ["consumerObserved"] = 0,
                        ["consumerOptionCount"] = definition.ConsumerActionIds.Count,
                    },
                });
            }
        }
    }

    private static void AnalyzePartnerEvidence(
        AnalyzerContext context,
        ActorRecord dancer,
        IAnalysisResultSink results)
    {
        var partnerStatusId = StatusId(DancerJobDefinition.DancePartnerStatus);
        var assignments = new List<PartnerInterval>();

        foreach (var target in context.Pull.Actors.OrderBy(actor => actor.Id.Value))
        {
            foreach (var interval in context.Statuses.ForActorStatus(target.Id, partnerStatusId)
                         .Where(interval => interval.Key.SourceActorId == dancer.Id))
            {
                assignments.Add(new PartnerInterval(target, interval));
            }
        }

        foreach (var group in assignments
                     .GroupBy(assignment => assignment.Target.Id)
                     .OrderBy(group => group.Key.Value))
        {
            var intervals = group.OrderBy(item => item.Interval.Start).ToArray();
            var target = intervals[0].Target;
            var range = new TimeRange(
                intervals.Min(item => item.Interval.Start),
                intervals.Max(item => item.Interval.End));
            var evidenceIds = intervals
                .SelectMany(item => item.Interval.EvidenceEventIds)
                .Distinct()
                .ToArray();
            var evidenceEvents = ResolveEvidenceEvents(context, evidenceIds);
            if (evidenceEvents.Count == 0)
            {
                continue;
            }

            results.Add(new AnalysisResult
            {
                Id = StableAnalysisResultIdentity.ForActorWindow(
                    context.Pull.Id,
                    AnalyzerId,
                    dancer.Id,
                    range,
                    $"partner-observed:{target.Id.Value}"),
                AnalyzerId = AnalyzerId,
                Severity = AnalysisSeverity.Info,
                Category = AnalysisCategory.Job,
                Title = $"{dancer.Name}: Dance Partner observed on {target.Name}",
                Summary =
                    $"Canonical Dance Partner status evidence links {dancer.Name} to {target.Name}. " +
                    "This records the observed assignment only; it does not rank partner quality or infer that missing prepull status evidence means no partner was assigned.",
                TimeRange = range,
                Actors = [dancer.Id, target.Id],
                Evidence =
                [
                    new AnalysisEvidence
                    {
                        EventIds = evidenceIds,
                        ActorIds = [dancer.Id, target.Id],
                        TimeRange = range,
                        Explanation = "Dance Partner status intervals identify the Dancer as source and the selected partner as target.",
                    },
                ],
                Confidence = EvidenceConfidence(evidenceEvents),
                Metrics = new Dictionary<string, double>
                {
                    ["partnerObserved"] = 1,
                    ["assignmentIntervalCount"] = intervals.Length,
                },
            });
        }

        AnalyzeKnownPartnerOverlaps(context, dancer, assignments, results);
    }

    private static void AnalyzeKnownPartnerOverlaps(
        AnalyzerContext context,
        ActorRecord dancer,
        IReadOnlyList<PartnerInterval> assignments,
        IAnalysisResultSink results)
    {
        for (var leftIndex = 0; leftIndex < assignments.Count; leftIndex++)
        {
            var left = assignments[leftIndex];
            if (!left.Interval.CoverageKnownThroughEnd)
            {
                continue;
            }

            for (var rightIndex = leftIndex + 1; rightIndex < assignments.Count; rightIndex++)
            {
                var right = assignments[rightIndex];
                if (left.Target.Id == right.Target.Id || !right.Interval.CoverageKnownThroughEnd)
                {
                    continue;
                }

                var overlapStart = left.Interval.Start > right.Interval.Start
                    ? left.Interval.Start
                    : right.Interval.Start;
                var overlapEnd = left.Interval.End < right.Interval.End
                    ? left.Interval.End
                    : right.Interval.End;
                if (overlapEnd <= overlapStart)
                {
                    continue;
                }

                var evidenceIds = left.Interval.EvidenceEventIds
                    .Concat(right.Interval.EvidenceEventIds)
                    .Distinct()
                    .ToArray();
                var evidenceEvents = ResolveEvidenceEvents(context, evidenceIds);
                if (evidenceEvents.Count == 0)
                {
                    continue;
                }

                var range = new TimeRange(overlapStart, overlapEnd);
                var orderedTargets = new[] { left.Target, right.Target }
                    .OrderBy(actor => actor.Id.Value)
                    .ToArray();
                results.Add(new AnalysisResult
                {
                    Id = StableAnalysisResultIdentity.ForActorWindow(
                        context.Pull.Id,
                        AnalyzerId,
                        dancer.Id,
                        range,
                        $"partner-overlap:{orderedTargets[0].Id.Value}:{orderedTargets[1].Id.Value}"),
                    AnalyzerId = AnalyzerId,
                    Severity = AnalysisSeverity.Warning,
                    Category = AnalysisCategory.Job,
                    Title = $"{dancer.Name}: conflicting Dance Partner evidence",
                    Summary =
                        $"Evidence-supported Dance Partner intervals from {dancer.Name} overlap on {orderedTargets[0].Name} and {orderedTargets[1].Name}. " +
                        "This is reported as contradictory assignment evidence, not as an optimal-partner judgment.",
                    TimeRange = range,
                    Actors = [dancer.Id, orderedTargets[0].Id, orderedTargets[1].Id],
                    Evidence =
                    [
                        new AnalysisEvidence
                        {
                            EventIds = evidenceIds,
                            ActorIds = [dancer.Id, orderedTargets[0].Id, orderedTargets[1].Id],
                            TimeRange = range,
                            Explanation =
                                "Two source-distinct canonical Dance Partner intervals from the same Dancer have known overlapping coverage on different targets.",
                        },
                    ],
                    Confidence = EvidenceConfidence(evidenceEvents),
                    Metrics = new Dictionary<string, double>
                    {
                        ["overlapSeconds"] = (overlapEnd - overlapStart).TotalSeconds,
                        ["conflictingPartnerCount"] = 2,
                    },
                });
            }
        }
    }

    private static bool CanUseAbsenceAsEvidence(AnalyzerContext context, StatusInterval interval)
    {
        if (context.Pull.Provenance.Fidelity != CaptureFidelity.Exact)
        {
            return false;
        }

        var evidence = ResolveEvidenceEvents(context, interval.EvidenceEventIds);
        return evidence.Count > 0 && evidence.All(evt => evt.Provenance.Fidelity == CaptureFidelity.Exact);
    }

    private static IReadOnlyList<NormalizedEvent> ResolveEvidenceEvents(
        AnalyzerContext context,
        IReadOnlyList<EventId> eventIds)
    {
        if (eventIds.Count == 0)
        {
            return Array.Empty<NormalizedEvent>();
        }

        var ids = eventIds.ToHashSet();
        return context.Events.All.Where(evt => ids.Contains(evt.Id)).ToArray();
    }

    private static float EvidenceConfidence(IEnumerable<NormalizedEvent> evidence)
    {
        var values = evidence.Select(evt => Math.Clamp(evt.Provenance.Confidence, 0.0f, 1.0f)).ToArray();
        return values.Length == 0 ? 0.0f : values.Min();
    }

    private static bool IsDancer(ActorRecord actor)
    {
        return string.Equals(actor.JobAbbreviation?.Trim(), DancerJobDefinition.JobAbbreviation, StringComparison.OrdinalIgnoreCase);
    }

    private static uint ActionId(string key)
    {
        return DancerJobDefinition.Definition.Action(key).ActionId;
    }

    private static uint StatusId(string key)
    {
        return DancerJobDefinition.Definition.Status(key).StatusId;
    }

    private static ProcConsumptionDefinition Proc(string statusKey, string name, params string[] consumerActionKeys)
    {
        return new ProcConsumptionDefinition(
            StatusId(statusKey),
            name,
            consumerActionKeys.Select(ActionId).ToHashSet());
    }

    private sealed record DanceFinishDefinition(string Name, int CompletedSteps);

    private sealed record ProcConsumptionDefinition(
        uint StatusId,
        string Name,
        IReadOnlySet<uint> ConsumerActionIds);

    private sealed record PartnerInterval(ActorRecord Target, StatusInterval Interval);
}
