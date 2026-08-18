namespace BetterDeaths.Analysis.Generic;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed class HealingActivityAnalyzer : IAnalyzerModule
{
    public const string AnalyzerId = "generic.healing-activity";

    public string Id => AnalyzerId;

    public AnalyzerScope Scope => AnalyzerScope.Generic;

    public IReadOnlyCollection<string> Dependencies => Array.Empty<string>();

    public bool Supports(AnalyzerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Events.OfType<HealEvent>().Count > 0;
    }

    public ValueTask AnalyzeAsync(
        AnalyzerContext context,
        IAnalysisResultSink results,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(results);

        var heals = context.Events.OfType<HealEvent>()
            .OrderBy(evt => evt.Sequence)
            .ToArray();
        foreach (var group in heals
                     .GroupBy(evt => evt.SourceActorId)
                     .OrderBy(group => group.Key?.Value ?? int.MaxValue))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var events = group.ToArray();
            if (events.Length == 0)
            {
                continue;
            }

            var sourceActorId = group.Key;
            var actorName = sourceActorId is { } actorId && context.Actors.TryGet(actorId, out var actor)
                ? actor?.Name
                : null;
            var range = new TimeRange(events[0].PullTime, events[^1].PullTime);
            var targetActors = events
                .Select(evt => evt.TargetActorId)
                .Where(actorId => actorId.HasValue)
                .Select(actorId => actorId!.Value)
                .Distinct()
                .OrderBy(actorId => actorId.Value)
                .ToArray();
            var actors = sourceActorId is { } source
                ? targetActors.Prepend(source).Distinct().ToArray()
                : targetActors;
            var totalRawHealing = events.Aggregate<HealEvent, ulong>(0, (total, evt) => total + evt.Amount);
            var confidence = events.Min(evt => Math.Clamp(evt.Provenance.Confidence, 0.0f, 1.0f));
            var resultId = sourceActorId is { } knownSource
                ? StableAnalysisResultIdentity.ForActorWindow(
                    context.Pull.Id,
                    AnalyzerId,
                    knownSource,
                    range,
                    "raw-healing-summary")
                : StableAnalysisResultIdentity.ForEvent(
                    context.Pull.Id,
                    AnalyzerId,
                    events[0].Id);

            results.Add(new AnalysisResult
            {
                Id = resultId,
                AnalyzerId = AnalyzerId,
                Severity = AnalysisSeverity.Info,
                Category = AnalysisCategory.Healing,
                Title = string.IsNullOrWhiteSpace(actorName)
                    ? "Healing activity observed"
                    : $"{actorName}: healing activity",
                Summary =
                    $"Captured {events.Length:N0} healing event(s) totaling {totalRawHealing:N0} raw healing across " +
                    $"{targetActors.Length:N0} target actor(s). Canonical HealEvent currently does not encode effective healing, " +
                    "overheal, HP deficit, MP/resource cost, or whether healing displaced a better action, so this is a neutral activity summary—not an overheal/waste judgment.",
                TimeRange = range,
                Actors = actors,
                Evidence =
                [
                    new AnalysisEvidence
                    {
                        EventIds = events.Select(evt => evt.Id).ToArray(),
                        ActorIds = actors,
                        TimeRange = range,
                        Explanation =
                            "Canonical HealEvent facts support raw event count/amount/targets. They do not support effective-heal, overheal, resource-efficiency, or opportunity-cost conclusions.",
                    },
                ],
                Confidence = confidence,
                Metrics = new Dictionary<string, double>
                {
                    ["healEventCount"] = events.Length,
                    ["rawHealingAmount"] = totalRawHealing,
                    ["uniqueTargetCount"] = targetActors.Length,
                    ["distinctActionCount"] = events.Select(evt => evt.ActionId).Distinct().Count(),
                    ["effectiveHealingKnown"] = 0,
                    ["overhealKnown"] = 0,
                    ["resourceCostKnown"] = 0,
                },
            });
        }

        return ValueTask.CompletedTask;
    }
}
