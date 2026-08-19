namespace BetterDeaths.Analysis.Generic;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Index;
using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed class GenericTimelineAnalyzer : IAnalyzerModule
{
    public const string AnalyzerId = "generic.explicit-timelines";

    private readonly IReadOnlyList<GenericTimelineDefinition> definitions;

    public GenericTimelineAnalyzer(IReadOnlyList<GenericTimelineDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var references = new HashSet<(GenericTimelineKind Kind, uint ReferenceId)>();
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            definition.Validate();
            if (!ids.Add(definition.Id))
            {
                throw new InvalidOperationException($"Duplicate timeline definition ID '{definition.Id}'.");
            }

            if (!references.Add((definition.Kind, definition.ReferenceId)))
            {
                throw new InvalidOperationException(
                    $"Timeline reference {definition.Kind}:{definition.ReferenceId} is configured more than once.");
            }
        }

        this.definitions = definitions.ToArray();
    }

    public string Id => AnalyzerId;

    public AnalyzerScope Scope => AnalyzerScope.Generic;

    public IReadOnlyCollection<string> Dependencies => Array.Empty<string>();

    public bool Supports(AnalyzerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (definitions.Count == 0)
        {
            return false;
        }

        return definitions.Any(definition => definition.Kind switch
        {
            GenericTimelineKind.CooldownAction => HasExplicitActionEvidence(context, definition.ReferenceId),
            GenericTimelineKind.BuffStatus => context.Events.ByStatus(definition.ReferenceId).Count > 0,
            _ => false,
        });
    }

    public ValueTask AnalyzeAsync(
        AnalyzerContext context,
        IAnalysisResultSink results,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(results);

        foreach (var definition in definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (definition.Kind)
            {
                case GenericTimelineKind.CooldownAction:
                    AnalyzeActionTimeline(context, definition, results);
                    break;
                case GenericTimelineKind.BuffStatus:
                    AnalyzeStatusTimeline(context, definition, results);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported generic timeline kind {definition.Kind}.");
            }
        }

        return ValueTask.CompletedTask;
    }

    private static void AnalyzeActionTimeline(
        AnalyzerContext context,
        GenericTimelineDefinition definition,
        IAnalysisResultSink results)
    {
        var evidence = GetExplicitActionEvidence(context, definition.ReferenceId);
        foreach (var group in evidence
                     .Where(evt => evt.SourceActorId is not null)
                     .GroupBy(evt => evt.SourceActorId!.Value)
                     .OrderBy(group => group.Key.Value))
        {
            var events = group.OrderBy(evt => evt.Sequence).ToArray();
            var actorId = group.Key;
            var actorName = context.Actors.TryGet(actorId, out var actor)
                ? actor?.Name
                : null;
            var range = new TimeRange(events[0].PullTime, events[^1].PullTime);
            var confidence = events.Min(evt => Math.Clamp(evt.Provenance.Confidence, 0.0f, 1.0f));
            var evidenceKind = events[0] is ActionUseEvent ? "ActionUseEvent" : "CastStartEvent fallback";

            results.Add(new AnalysisResult
            {
                Id = StableAnalysisResultIdentity.ForActorWindow(
                    context.Pull.Id,
                    AnalyzerId,
                    actorId,
                    range,
                    definition.Id),
                AnalyzerId = AnalyzerId,
                Severity = AnalysisSeverity.Info,
                Category = AnalysisCategory.Cooldown,
                Title = string.IsNullOrWhiteSpace(actorName)
                    ? $"{definition.Name}: action timeline"
                    : $"{actorName}: {definition.Name} timeline",
                Summary =
                    $"Observed {events.Length:N0} explicit {evidenceKind} record(s) for configured action {definition.Name} ({definition.ReferenceId}). " +
                    "Fallback is chosen independently per source actor, so one actor's ActionUse evidence cannot hide another actor's CastStart evidence. " +
                    "This timeline does not infer expected uses, cooldown availability, alignment quality, or missed usage; those require job/encounter semantics or explicit availability evidence.",
                TimeRange = range,
                Actors = [actorId],
                Evidence =
                [
                    new AnalysisEvidence
                    {
                        EventIds = events.Select(evt => evt.Id).ToArray(),
                        ActorIds = [actorId],
                        TimeRange = range,
                        Explanation =
                            "Only explicit canonical action-use evidence is counted per actor; CastStartEvent is used for an actor only when that actor has no ActionUseEvent for the configured action. Damage/heal packets are not reinterpreted as extra uses.",
                    },
                ],
                Confidence = confidence,
                Metrics = new Dictionary<string, double>
                {
                    ["observedUseEvidenceCount"] = events.Length,
                    ["expectedUsesKnown"] = 0,
                    ["availabilityKnown"] = 0,
                    ["missedUseClaimed"] = 0,
                    ["actionId"] = definition.ReferenceId,
                },
            });
        }
    }

    private static void AnalyzeStatusTimeline(
        AnalyzerContext context,
        GenericTimelineDefinition definition,
        IAnalysisResultSink results)
    {
        foreach (var actor in context.Pull.Actors.OrderBy(actor => actor.Id.Value))
        {
            var intervals = context.Statuses.ForActorStatus(actor.Id, definition.ReferenceId)
                .OrderBy(interval => interval.Start)
                .ThenBy(interval => interval.Key.SourceActorId?.Value ?? int.MinValue)
                .ToArray();
            if (intervals.Length == 0)
            {
                continue;
            }

            var known = intervals.Where(interval => interval.CoverageKnownThroughEnd).ToArray();
            var uncertain = intervals.Where(interval => !interval.CoverageKnownThroughEnd).ToArray();
            var range = new TimeRange(
                intervals.Min(interval => interval.Start),
                intervals.Max(interval => interval.End));
            var evidenceIds = intervals
                .SelectMany(interval => interval.EvidenceEventIds)
                .Distinct()
                .ToArray();
            var sourceActors = intervals
                .Select(interval => interval.Key.SourceActorId)
                .Where(source => source.HasValue)
                .Select(source => source!.Value)
                .Distinct()
                .OrderBy(source => source.Value)
                .ToArray();
            var actors = sourceActors.Prepend(actor.Id).Distinct().ToArray();
            var confidence = GetStatusEvidenceConfidence(context, definition.ReferenceId, evidenceIds);

            results.Add(new AnalysisResult
            {
                Id = StableAnalysisResultIdentity.ForActorWindow(
                    context.Pull.Id,
                    AnalyzerId,
                    actor.Id,
                    range,
                    definition.Id),
                AnalyzerId = AnalyzerId,
                Severity = AnalysisSeverity.Info,
                Category = AnalysisCategory.Buff,
                Title = $"{actor.Name}: {definition.Name} status timeline",
                Summary =
                    $"Observed {intervals.Length:N0} configured {definition.Name} status interval(s): {known.Length:N0} with evidence-supported end coverage and {uncertain.Length:N0} with uncertain ending coverage. " +
                    "This is a configured status timeline, not a buff-uptime optimization or missed-refresh verdict.",
                TimeRange = range,
                Actors = actors,
                Evidence =
                [
                    new AnalysisEvidence
                    {
                        EventIds = evidenceIds,
                        ActorIds = actors,
                        TimeRange = range,
                        Explanation =
                            "Canonical status apply/remove/duration evidence supports the displayed configured status intervals. Unknown interval ends remain uncertain rather than being treated as full uptime.",
                    },
                ],
                Confidence = confidence,
                Metrics = new Dictionary<string, double>
                {
                    ["statusIntervalCount"] = intervals.Length,
                    ["knownEndIntervalCount"] = known.Length,
                    ["uncertainEndIntervalCount"] = uncertain.Length,
                    ["expectedUptimeKnown"] = 0,
                    ["missedRefreshClaimed"] = 0,
                    ["statusId"] = definition.ReferenceId,
                },
            });
        }
    }

    private static IReadOnlyList<NormalizedEvent> GetExplicitActionEvidence(AnalyzerContext context, uint actionId)
    {
        var explicitEvidence = context.Events.ByAction(actionId)
            .Where(evt => evt.SourceActorId is not null && evt is ActionUseEvent or CastStartEvent)
            .GroupBy(evt => evt.SourceActorId!.Value)
            .OrderBy(group => group.Key.Value);
        var selected = new List<NormalizedEvent>();

        foreach (var group in explicitEvidence)
        {
            var actionUses = group
                .OfType<ActionUseEvent>()
                .Cast<NormalizedEvent>()
                .OrderBy(evt => evt.Sequence)
                .ToArray();
            if (actionUses.Length > 0)
            {
                selected.AddRange(actionUses);
                continue;
            }

            selected.AddRange(group
                .OfType<CastStartEvent>()
                .Cast<NormalizedEvent>()
                .OrderBy(evt => evt.Sequence));
        }

        return selected.OrderBy(evt => evt.Sequence).ToArray();
    }

    private static bool HasExplicitActionEvidence(AnalyzerContext context, uint actionId)
    {
        return GetExplicitActionEvidence(context, actionId).Count > 0;
    }

    private static float GetStatusEvidenceConfidence(
        AnalyzerContext context,
        uint statusId,
        IReadOnlyCollection<EventId> evidenceIds)
    {
        var ids = evidenceIds.ToHashSet();
        var confidence = 1.0f;
        var found = false;
        foreach (var evt in context.Events.ByStatus(statusId))
        {
            if (!ids.Contains(evt.Id))
            {
                continue;
            }

            found = true;
            confidence = Math.Min(confidence, Math.Clamp(evt.Provenance.Confidence, 0.0f, 1.0f));
        }

        return found ? confidence : 0.0f;
    }
}
