namespace BetterDeaths.Analysis.Generic;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

internal sealed class DeathEventAnalyzer : IAnalyzerModule
{
    public const string AnalyzerId = "generic.death-events";

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

        foreach (var death in context.Events.OfType<DeathEvent>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var actorIds = death.TargetActorId is { } targetActorId
                ? new[] { targetActorId }
                : Array.Empty<ActorId>();
            var actorName = death.TargetActorId is { } actorId && context.Actors.TryGet(actorId, out var actor)
                ? actor?.Name
                : null;
            var timeRange = new TimeRange(death.PullTime, death.PullTime);
            var confidence = Math.Clamp(death.Provenance.Confidence, 0.0f, 1.0f);

            results.Add(new AnalysisResult
            {
                Id = StableAnalysisResultIdentity.ForEvent(context.Pull.Id, AnalyzerId, death.Id),
                AnalyzerId = AnalyzerId,
                Severity = AnalysisSeverity.Error,
                Category = AnalysisCategory.Death,
                Title = string.IsNullOrWhiteSpace(actorName)
                    ? "Death observed"
                    : $"{actorName} died",
                Summary = "A canonical death event was observed. Causal mistake classification is deferred to the full death-analysis milestone.",
                TimeRange = timeRange,
                Actors = actorIds,
                Evidence =
                [
                    new AnalysisEvidence
                    {
                        EventIds = [death.Id],
                        ActorIds = actorIds,
                        TimeRange = timeRange,
                        Explanation = "Canonical DeathEvent evidence for this finding.",
                    },
                ],
                Confidence = confidence,
                Metrics = new Dictionary<string, double>
                {
                    ["pullTimeSeconds"] = death.PullTime.TotalSeconds,
                },
            });
        }

        return ValueTask.CompletedTask;
    }
}
