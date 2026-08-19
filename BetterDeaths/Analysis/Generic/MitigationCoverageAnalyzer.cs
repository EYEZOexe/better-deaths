namespace BetterDeaths.Analysis.Generic;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Index;
using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed class MitigationCoverageAnalyzer : IAnalyzerModule
{
    public const string AnalyzerId = "generic.mitigation-coverage";

    private readonly IReadOnlyList<MitigationDefinition> definitions;

    public MitigationCoverageAnalyzer(IReadOnlyList<MitigationDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var statusApplications = new HashSet<(uint StatusId, MitigationApplicationKind ApplicationKind)>();
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            definition.Validate();
            if (!ids.Add(definition.Id))
            {
                throw new InvalidOperationException($"Duplicate mitigation definition ID '{definition.Id}'.");
            }

            if (!statusApplications.Add((definition.StatusId, definition.ApplicationKind)))
            {
                throw new InvalidOperationException(
                    $"Mitigation status {definition.StatusId} with application kind {definition.ApplicationKind} is configured more than once.");
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
        return definitions.Count > 0 && context.Events.OfType<DamageEvent>().Count > 0;
    }

    public ValueTask AnalyzeAsync(
        AnalyzerContext context,
        IAnalysisResultSink results,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(results);

        foreach (var damage in context.Events.OfType<DamageEvent>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (damage.Amount < 0)
            {
                continue;
            }

            var active = GetActiveMitigations(context, damage);
            if (active.Count == 0)
            {
                // Absence is not a missed-use finding. Canonical status evidence may be incomplete,
                // and ability availability/recent use are separate facts that this analyzer does not have.
                continue;
            }

            var targetName = ResolveActorName(context, damage.TargetActorId);
            var sourceName = ResolveActorName(context, damage.SourceActorId);
            var overlap = active.Count > 1;
            var evidenceIds = active
                .SelectMany(entry => entry.Intervals.SelectMany(interval => interval.EvidenceEventIds))
                .Prepend(damage.Id)
                .Distinct()
                .ToArray();
            var actors = new[] { damage.SourceActorId, damage.TargetActorId }
                .Concat(active.SelectMany(entry => entry.Intervals.Select(interval => interval.Key.SourceActorId)))
                .Where(actorId => actorId.HasValue)
                .Select(actorId => actorId!.Value)
                .Distinct()
                .OrderBy(actorId => actorId.Value)
                .ToArray();
            var configuredReduction = GetConfiguredCombinedReduction(active);
            var estimate = configuredReduction is { } reduction
                ? EstimateWithoutModeledReduction(damage.Amount, reduction)
                : null;
            var confidence = GetConfidence(context, damage, active);
            var mitigationNames = string.Join(
                ", ",
                active.Select(entry => $"{entry.Definition.Name} ({GetScopeLabel(entry.Definition.ScopeKind)})"));
            var targetLabel = string.IsNullOrWhiteSpace(targetName) ? "target" : targetName;
            var sourceLabel = string.IsNullOrWhiteSpace(sourceName) ? "damage source" : sourceName;

            var summary = overlap
                ? $"Observed {active.Count} configured mitigation effect(s) overlapping this {damage.Amount:N0} damage event on {targetLabel}: {mitigationNames}. Overlap is coverage evidence, not automatically waste."
                : $"Observed configured mitigation coverage on this {damage.Amount:N0} damage event on {targetLabel}: {mitigationNames}.";
            summary +=
                " Scope is retained from the configured mitigation definition; target-status evidence does not collapse personal, targeted, and party-wide semantics into one meaning.";
            summary +=
                " This finding does not claim that an absent mitigation was available, that this coverage was optimal, or that a cooldown was missed.";
            if (estimate is not null)
            {
                summary +=
                    $" Under the explicit assumption that the configured reductions are multiplicative and the observed damage on {targetLabel} from {sourceLabel} is post-mitigation, the modeled pre-reduction amount is approximately {estimate.Value.EstimatedWithoutModeledReduction:N0}. This is a counterfactual estimate, not reconstructed server damage or a survival claim.";
            }

            var metrics = new Dictionary<string, double>
            {
                ["observedDamageAmount"] = damage.Amount,
                ["activeMitigationCount"] = active.Count,
                ["overlapObserved"] = overlap ? 1 : 0,
                ["availabilityKnown"] = 0,
                ["missedUseClaimed"] = 0,
            };
            AddScopeMetrics(metrics, active);
            if (configuredReduction is { } combinedReduction)
            {
                metrics["configuredCombinedReductionFraction"] = combinedReduction;
            }

            if (estimate is { } whatIf)
            {
                metrics["whatIfEstimateAvailable"] = 1;
                metrics["estimatedWithoutModeledReduction"] = whatIf.EstimatedWithoutModeledReduction;
                metrics["estimatedModeledReductionAmount"] = whatIf.EstimatedModeledReductionAmount;
            }
            else
            {
                metrics["whatIfEstimateAvailable"] = 0;
            }

            results.Add(new AnalysisResult
            {
                Id = StableAnalysisResultIdentity.ForEvent(context.Pull.Id, AnalyzerId, damage.Id),
                AnalyzerId = AnalyzerId,
                Severity = overlap ? AnalysisSeverity.Observation : AnalysisSeverity.Info,
                Category = AnalysisCategory.Mitigation,
                Title = overlap
                    ? $"{targetLabel}: mitigation overlap observed"
                    : $"{targetLabel}: mitigation coverage observed",
                Summary = summary,
                TimeRange = new TimeRange(damage.PullTime, damage.PullTime),
                Actors = actors,
                Evidence =
                [
                    new AnalysisEvidence
                    {
                        EventIds = evidenceIds,
                        ActorIds = actors,
                        TimeRange = new TimeRange(damage.PullTime, damage.PullTime),
                        Explanation =
                            "The damage event plus evidence-supported active status intervals establish configured mitigation coverage at this timestamp. Configured scope/effect semantics are retained separately from where the status evidence is observed. Absence/availability is not inferred from this evidence.",
                    },
                ],
                Confidence = confidence,
                Metrics = metrics,
            });
        }

        return ValueTask.CompletedTask;
    }

    private IReadOnlyList<ActiveMitigation> GetActiveMitigations(AnalyzerContext context, DamageEvent damage)
    {
        var active = new List<ActiveMitigation>();
        foreach (var definition in definitions)
        {
            var actorId = definition.ApplicationKind switch
            {
                MitigationApplicationKind.TargetStatus => damage.TargetActorId,
                MitigationApplicationKind.DamageSourceStatus => damage.SourceActorId,
                _ => null,
            };
            if (actorId is null)
            {
                continue;
            }

            var intervals = context.Statuses
                .ForActorStatus(actorId.Value, definition.StatusId)
                .Where(interval => interval.CoverageKnownThroughEnd)
                .Where(interval => damage.PullTime >= interval.Start && damage.PullTime < interval.End)
                .ToArray();
            if (intervals.Length > 0)
            {
                active.Add(new ActiveMitigation(definition, intervals));
            }
        }

        return active;
    }

    private static void AddScopeMetrics(
        IDictionary<string, double> metrics,
        IReadOnlyList<ActiveMitigation> active)
    {
        metrics["activePersonalMitigationCount"] = active.Count(entry => entry.Definition.ScopeKind == MitigationScopeKind.Personal);
        metrics["activeTargetedMitigationCount"] = active.Count(entry => entry.Definition.ScopeKind == MitigationScopeKind.Targeted);
        metrics["activePartyWideMitigationCount"] = active.Count(entry => entry.Definition.ScopeKind == MitigationScopeKind.PartyWide);
        metrics["activeDamageSourceDebuffCount"] = active.Count(entry => entry.Definition.ScopeKind == MitigationScopeKind.DamageSourceDebuff);
        metrics["activeOtherScopeMitigationCount"] = active.Count(entry => entry.Definition.ScopeKind == MitigationScopeKind.Other);
    }

    private static string GetScopeLabel(MitigationScopeKind scope)
    {
        return scope switch
        {
            MitigationScopeKind.Personal => "personal",
            MitigationScopeKind.Targeted => "targeted",
            MitigationScopeKind.PartyWide => "party-wide",
            MitigationScopeKind.DamageSourceDebuff => "damage-source debuff",
            MitigationScopeKind.Other => "other scope",
            _ => scope.ToString(),
        };
    }

    private static double? GetConfiguredCombinedReduction(IReadOnlyList<ActiveMitigation> active)
    {
        var reductions = active
            .Select(entry => entry.Definition)
            .Where(definition => definition.EffectKind == MitigationEffectKind.DamageReduction)
            .Select(definition => definition.DamageReductionFraction)
            .Where(reduction => reduction.HasValue)
            .Select(reduction => reduction!.Value)
            .ToArray();
        if (reductions.Length == 0)
        {
            return null;
        }

        var remaining = 1.0;
        foreach (var reduction in reductions)
        {
            remaining *= 1.0 - reduction;
        }

        return 1.0 - remaining;
    }

    private static MitigationEstimate? EstimateWithoutModeledReduction(long observedDamage, double combinedReduction)
    {
        var remaining = 1.0 - combinedReduction;
        if (observedDamage < 0 || remaining <= 0.0 || remaining >= 1.0)
        {
            return null;
        }

        var estimatedWithout = observedDamage / remaining;
        return new MitigationEstimate(
            estimatedWithout,
            estimatedWithout - observedDamage);
    }

    private static float GetConfidence(
        AnalyzerContext context,
        DamageEvent damage,
        IReadOnlyList<ActiveMitigation> active)
    {
        var confidence = Math.Clamp(damage.Provenance.Confidence, 0.0f, 1.0f);
        foreach (var entry in active)
        {
            var evidenceIds = entry.Intervals
                .SelectMany(interval => interval.EvidenceEventIds)
                .ToHashSet();
            foreach (var evt in context.Events.ByStatus(entry.Definition.StatusId))
            {
                if (evidenceIds.Contains(evt.Id))
                {
                    confidence = Math.Min(confidence, Math.Clamp(evt.Provenance.Confidence, 0.0f, 1.0f));
                }
            }
        }

        return confidence;
    }

    private static string? ResolveActorName(AnalyzerContext context, ActorId? actorId)
    {
        return actorId is { } id && context.Actors.TryGet(id, out var actor)
            ? actor?.Name
            : null;
    }

    private sealed record ActiveMitigation(
        MitigationDefinition Definition,
        IReadOnlyList<StatusInterval> Intervals);

    private readonly record struct MitigationEstimate(
        double EstimatedWithoutModeledReduction,
        double EstimatedModeledReductionAmount);
}
