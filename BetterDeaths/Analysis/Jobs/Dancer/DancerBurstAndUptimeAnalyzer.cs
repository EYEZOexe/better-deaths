namespace BetterDeaths.Analysis.Jobs.Dancer;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Index;
using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed class DancerBurstAndUptimeAnalyzer : IAnalyzerModule
{
    public const string AnalyzerId = "job.dnc.burst-uptime";
    public const string DevilmentOutsideTechnicalRuleKey = "devilment.outside-technical-window";
    public const string DevilmentDelayedTechnicalRuleKey = "devilment.delayed-inside-technical";
    public const string CooldownAdditionalOpportunityRulePrefix = "cooldown.additional-opportunity";
    public const string CooldownActiveDriftRulePrefix = "cooldown.active-drift";
    public const string TargetableGcdGapRuleKey = "gcd.targetable-gap";

    private static readonly TimeSpan TechnicalWindowDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CooldownDriftGrace = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MinimumGcdGap = TimeSpan.FromSeconds(5);

    private static readonly IReadOnlySet<uint> TechnicalFinishActionIds = new HashSet<uint>
    {
        ActionId(DancerJobDefinition.TechnicalFinish),
        ActionId(DancerJobDefinition.SingleTechnicalFinish),
        ActionId(DancerJobDefinition.DoubleTechnicalFinish),
        ActionId(DancerJobDefinition.TripleTechnicalFinish),
        ActionId(DancerJobDefinition.QuadrupleTechnicalFinish),
    };

    private static readonly IReadOnlySet<uint> DancerGcdActionIds = DancerJobDefinition.Definition.Actions
        .Where(action => action.IsGcd)
        .Select(action => action.ActionId)
        .ToHashSet();

    private static readonly IReadOnlyList<CooldownCadenceDefinition> CadenceDefinitions =
    [
        Cooldown(DancerJobDefinition.TechnicalStep, "Technical Step"),
        Cooldown(DancerJobDefinition.Devilment, "Devilment"),
        Cooldown(DancerJobDefinition.Flourish, "Flourish"),
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

        var allActionUses = context.Events.OfType<ActionUseEvent>();
        foreach (var dancer in context.Pull.Actors.Where(IsDancer).OrderBy(actor => actor.Id.Value))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var actorActions = allActionUses
                .Where(evt => evt.SourceActorId == dancer.Id)
                .OrderBy(evt => evt.Sequence)
                .ToArray();

            AnalyzeDevilmentAlignment(context, dancer, actorActions, results);

            var primaryTarget = SelectPrimaryEnemy(context, dancer);
            if (primaryTarget is null)
            {
                continue;
            }

            AnalyzeCooldownCadence(context, dancer, primaryTarget, actorActions, results);
            AnalyzeTargetableGcdGaps(context, dancer, primaryTarget, actorActions, results);
        }

        return ValueTask.CompletedTask;
    }

    private static void AnalyzeDevilmentAlignment(
        AnalyzerContext context,
        ActorRecord dancer,
        IReadOnlyList<ActionUseEvent> actions,
        IAnalysisResultSink results)
    {
        var devilmentActionId = ActionId(DancerJobDefinition.Devilment);
        var finishes = actions
            .Where(action => TechnicalFinishActionIds.Contains(action.ActionId))
            .ToArray();

        foreach (var devilment in actions.Where(action => action.ActionId == devilmentActionId))
        {
            var finish = finishes
                .Where(candidate => candidate.PullTime <= devilment.PullTime)
                .OrderByDescending(candidate => candidate.PullTime)
                .ThenByDescending(candidate => candidate.Sequence)
                .FirstOrDefault();

            // Without an observed preceding finish we cannot distinguish a true alignment error from
            // a pull/source boundary that omitted the relevant Technical Finish.
            if (finish is null)
            {
                continue;
            }

            var elapsed = devilment.PullTime - finish.PullTime;
            if (elapsed > TechnicalWindowDuration)
            {
                if (!CanUseActionAbsenceAsEvidence(context, finish, devilment))
                {
                    continue;
                }

                var outsideRange = new TimeRange(finish.PullTime, devilment.PullTime);
                results.Add(new AnalysisResult
                {
                    Id = StableAnalysisResultIdentity.ForActorWindow(
                        context.Pull.Id,
                        AnalyzerId,
                        dancer.Id,
                        outsideRange,
                        $"devilment-outside-technical:{finish.Id.Value}:{devilment.Id.Value}"),
                    AnalyzerId = AnalyzerId,
                    RuleKey = DevilmentOutsideTechnicalRuleKey,
                    Severity = AnalysisSeverity.Warning,
                    Category = AnalysisCategory.Job,
                    Title = $"{dancer.Name}: Devilment after the observed Technical Finish window",
                    Summary =
                        $"Devilment occurred {elapsed.TotalSeconds:F1}s after an observed Technical Finish, beyond its configured {TechnicalWindowDuration.TotalSeconds:F0}s window. " +
                        "Both boundary actions and the pull are exact. If no preceding Technical Finish is observed at all, this analyzer stays silent rather than using missing boundary evidence as proof.",
                    TimeRange = outsideRange,
                    Actors = [dancer.Id],
                    Evidence =
                    [
                        new AnalysisEvidence
                        {
                            EventIds = [finish.Id, devilment.Id],
                            ActorIds = [dancer.Id],
                            TimeRange = outsideRange,
                            Explanation =
                                "An explicit Technical Finish and later explicit Devilment bound the alignment error; the elapsed time exceeds the defined Technical Finish duration.",
                        },
                    ],
                    Confidence = EvidenceConfidence([finish, devilment]),
                    Metrics = new Dictionary<string, double>
                    {
                        ["technicalWindowSeconds"] = TechnicalWindowDuration.TotalSeconds,
                        ["devilmentDelaySeconds"] = elapsed.TotalSeconds,
                        ["technicalFinishObserved"] = 1,
                    },
                });
                continue;
            }

            var interveningActions = actions
                .Where(action => action.Sequence > finish.Sequence && action.Sequence < devilment.Sequence)
                .Where(action => action.PullTime >= finish.PullTime && action.PullTime <= devilment.PullTime)
                .ToArray();
            if (interveningActions.Length == 0)
            {
                continue;
            }

            var evidenceEvents = new List<NormalizedEvent> { finish };
            evidenceEvents.AddRange(interveningActions);
            evidenceEvents.Add(devilment);
            var delayedRange = new TimeRange(finish.PullTime, devilment.PullTime);
            results.Add(new AnalysisResult
            {
                Id = StableAnalysisResultIdentity.ForActorWindow(
                    context.Pull.Id,
                    AnalyzerId,
                    dancer.Id,
                    delayedRange,
                    $"devilment-delayed:{finish.Id.Value}:{devilment.Id.Value}"),
                AnalyzerId = AnalyzerId,
                RuleKey = DevilmentDelayedTechnicalRuleKey,
                Severity = AnalysisSeverity.Optimization,
                Category = AnalysisCategory.Job,
                Title = $"{dancer.Name}: Devilment delayed inside Technical Finish",
                Summary =
                    $"Devilment was used after {interveningActions.Length:N0} other Dancer action(s) following the observed Technical Finish. " +
                    "The Dancer reference rule is to use Devilment immediately after Technical Finish so its 20-second buff overlaps the Technical window as fully as possible. " +
                    "This finding compares explicit action ordering rather than estimating animation-lock or network timing.",
                TimeRange = delayedRange,
                Actors = [dancer.Id],
                Evidence =
                [
                    new AnalysisEvidence
                    {
                        EventIds = evidenceEvents.Select(evt => evt.Id).ToArray(),
                        ActorIds = [dancer.Id],
                        TimeRange = delayedRange,
                        Explanation =
                            "Technical Finish is followed by one or more explicit Dancer actions before the explicit Devilment use.",
                    },
                ],
                Confidence = EvidenceConfidence(evidenceEvents),
                Metrics = new Dictionary<string, double>
                {
                    ["interveningActionCount"] = interveningActions.Length,
                    ["devilmentDelaySeconds"] = elapsed.TotalSeconds,
                    ["technicalFinishActionId"] = finish.ActionId,
                },
            });
        }
    }

    private static void AnalyzeCooldownCadence(
        AnalyzerContext context,
        ActorRecord dancer,
        ActorRecord primaryTarget,
        IReadOnlyList<ActionUseEvent> actions,
        IAnalysisResultSink results)
    {
        foreach (var definition in CadenceDefinitions)
        {
            var uses = actions
                .Where(action => action.ActionId == definition.ActionId)
                .OrderBy(action => action.Sequence)
                .ToArray();
            if (uses.Length == 0)
            {
                // No first-use readiness is inferred from pull start or prepull state.
                continue;
            }

            for (var index = 1; index < uses.Length; index++)
            {
                var previous = uses[index - 1];
                var current = uses[index];
                if (!CanUseActionAbsenceAsEvidence(context, previous, current))
                {
                    continue;
                }

                var range = new TimeRange(previous.PullTime, current.PullTime);
                if (ContainsDeath(context, dancer.Id, range))
                {
                    continue;
                }

                var coverage = context.Targetability.GetCoverage(primaryTarget.Id, range);
                var activeDrift = coverage.TargetableDuration - definition.Cooldown;
                if (activeDrift <= CooldownDriftGrace)
                {
                    continue;
                }

                AddCooldownOpportunityResult(
                    context,
                    dancer,
                    primaryTarget,
                    definition,
                    previous,
                    current,
                    range,
                    coverage,
                    activeDrift,
                    terminalOpportunity: false,
                    results);
            }

            var lastUse = uses[^1];
            if (lastUse.PullTime >= context.Pull.Metadata.Duration)
            {
                continue;
            }

            var terminalRange = new TimeRange(lastUse.PullTime, context.Pull.Metadata.Duration);
            if (ContainsDeath(context, dancer.Id, terminalRange))
            {
                continue;
            }

            var terminalCoverage = context.Targetability.GetCoverage(primaryTarget.Id, terminalRange);
            var terminalDrift = terminalCoverage.TargetableDuration - definition.Cooldown;
            if (terminalDrift <= CooldownDriftGrace || !CanUseActionAbsenceAsEvidence(context, lastUse))
            {
                continue;
            }

            AddCooldownOpportunityResult(
                context,
                dancer,
                primaryTarget,
                definition,
                lastUse,
                currentUse: null,
                terminalRange,
                terminalCoverage,
                terminalDrift,
                terminalOpportunity: true,
                results);
        }
    }

    private static void AddCooldownOpportunityResult(
        AnalyzerContext context,
        ActorRecord dancer,
        ActorRecord primaryTarget,
        CooldownCadenceDefinition definition,
        ActionUseEvent previousUse,
        ActionUseEvent? currentUse,
        TimeRange range,
        TargetabilityCoverage coverage,
        TimeSpan activeDrift,
        bool terminalOpportunity,
        IAnalysisResultSink results)
    {
        var evidenceIds = coverage.EvidenceEventIds
            .Prepend(previousUse.Id)
            .Concat(currentUse is null ? Array.Empty<EventId>() : new[] { currentUse.Id })
            .Distinct()
            .ToArray();
        var evidenceEvents = ResolveEvidenceEvents(context, evidenceIds);
        if (evidenceEvents.Count == 0)
        {
            return;
        }

        var title = terminalOpportunity
            ? $"{dancer.Name}: additional {definition.Name} opportunity observed"
            : $"{dancer.Name}: {definition.Name} drift during targetable time";
        var summary = terminalOpportunity
            ? $"After the last observed {definition.Name}, at least {coverage.TargetableDuration.TotalSeconds:F1}s of {primaryTarget.Name} targetable time was evidence-supported. " +
              $"That exceeds the configured {definition.Cooldown.TotalSeconds:F0}s cooldown by {activeDrift.TotalSeconds:F1}s, even after ignoring forced untargetable and unknown time. " +
              "Because a prior use establishes the cooldown timer and the pull/action evidence is exact, this is a conservative additional-use opportunity rather than a pull-duration estimate."
            : $"Between two observed {definition.Name} uses, {coverage.TargetableDuration.TotalSeconds:F1}s of {primaryTarget.Name} targetable time was evidence-supported. " +
              $"That exceeds the configured {definition.Cooldown.TotalSeconds:F0}s cooldown by {activeDrift.TotalSeconds:F1}s after forced untargetable and unknown time are excluded. " +
              "Both boundary actions and the pull are exact, so no hidden use is assumed between them. The result reports conservative active-time drift rather than charging encounter downtime as ordinary execution loss.";

        results.Add(new AnalysisResult
        {
            Id = StableAnalysisResultIdentity.ForActorWindow(
                context.Pull.Id,
                AnalyzerId,
                dancer.Id,
                range,
                terminalOpportunity
                    ? $"cooldown-terminal:{definition.Key}:{previousUse.Id.Value}"
                    : $"cooldown-drift:{definition.Key}:{previousUse.Id.Value}:{currentUse!.Id.Value}"),
            AnalyzerId = AnalyzerId,
            RuleKey = terminalOpportunity
                ? $"{CooldownAdditionalOpportunityRulePrefix}.{definition.Key}"
                : $"{CooldownActiveDriftRulePrefix}.{definition.Key}",
            Severity = terminalOpportunity ? AnalysisSeverity.Warning : AnalysisSeverity.Optimization,
            Category = AnalysisCategory.Job,
            Title = title,
            Summary = summary,
            TimeRange = range,
            Actors = [dancer.Id, primaryTarget.Id],
            Evidence =
            [
                new AnalysisEvidence
                {
                    EventIds = evidenceIds,
                    ActorIds = [dancer.Id, primaryTarget.Id],
                    TimeRange = range,
                    Explanation =
                        "The previous cooldown use establishes readiness timing. Targetability transition evidence supplies only the known active-time portion of the window; untargetable and unknown time are excluded from the opportunity calculation.",
                },
            ],
            Confidence = EvidenceConfidence(evidenceEvents),
            Metrics = new Dictionary<string, double>
            {
                ["cooldownSeconds"] = definition.Cooldown.TotalSeconds,
                ["knownTargetableSeconds"] = coverage.TargetableDuration.TotalSeconds,
                ["knownUntargetableSeconds"] = coverage.UntargetableDuration.TotalSeconds,
                ["unknownSeconds"] = coverage.UnknownDuration.TotalSeconds,
                ["activeDriftSeconds"] = activeDrift.TotalSeconds,
                ["terminalOpportunity"] = terminalOpportunity ? 1 : 0,
                ["actionId"] = definition.ActionId,
            },
        });
    }

    private static void AnalyzeTargetableGcdGaps(
        AnalyzerContext context,
        ActorRecord dancer,
        ActorRecord primaryTarget,
        IReadOnlyList<ActionUseEvent> actions,
        IAnalysisResultSink results)
    {
        var gcds = actions
            .Where(action => DancerGcdActionIds.Contains(action.ActionId))
            .OrderBy(action => action.Sequence)
            .ToArray();
        if (gcds.Length < 2)
        {
            return;
        }

        var targetableIntervals = context.Targetability.ForActor(primaryTarget.Id)
            .Where(interval => interval.IsTargetable && interval.Duration > TimeSpan.Zero)
            .ToArray();
        if (targetableIntervals.Length == 0)
        {
            return;
        }

        for (var index = 1; index < gcds.Length; index++)
        {
            var previous = gcds[index - 1];
            var next = gcds[index];
            if (next.PullTime <= previous.PullTime ||
                !CanUseActionAbsenceAsEvidence(context, previous, next))
            {
                continue;
            }

            var pairRange = new TimeRange(previous.PullTime, next.PullTime);
            if (ContainsDeath(context, dancer.Id, pairRange))
            {
                continue;
            }

            foreach (var interval in targetableIntervals)
            {
                var gapStart = previous.PullTime > interval.Start ? previous.PullTime : interval.Start;
                var gapEnd = next.PullTime < interval.End ? next.PullTime : interval.End;
                var targetableGap = gapEnd - gapStart;
                if (targetableGap < MinimumGcdGap)
                {
                    continue;
                }

                var range = new TimeRange(gapStart, gapEnd);
                var evidenceIds = interval.EvidenceEventIds
                    .Append(previous.Id)
                    .Append(next.Id)
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
                        $"gcd-gap:{primaryTarget.Id.Value}:{previous.Id.Value}:{next.Id.Value}"),
                    AnalyzerId = AnalyzerId,
                    RuleKey = TargetableGcdGapRuleKey,
                    Severity = AnalysisSeverity.Optimization,
                    Category = AnalysisCategory.Job,
                    Title = $"{dancer.Name}: {targetableGap.TotalSeconds:F1}s targetable GCD gap",
                    Summary =
                        $"Two exact Dancer GCD actions bound a {targetableGap.TotalSeconds:F1}s gap while {primaryTarget.Name} was evidence-supported as targetable. " +
                        "Only the targetable intersection is reported; forced untargetable time, unknown targetability, terminal pull time, and death-containing gaps are not converted into Dancer inactivity. " +
                        "This is an execution-gap observation, not a simulated Skill Speed/GCD-count model.",
                    TimeRange = range,
                    Actors = [dancer.Id, primaryTarget.Id],
                    Evidence =
                    [
                        new AnalysisEvidence
                        {
                            EventIds = evidenceIds,
                            ActorIds = [dancer.Id, primaryTarget.Id],
                            TimeRange = range,
                            Explanation =
                                "The surrounding exact Dancer GCD actions bound the gap and canonical targetability transitions prove this portion occurred while the selected enemy was targetable.",
                        },
                    ],
                    Confidence = EvidenceConfidence(evidenceEvents),
                    Metrics = new Dictionary<string, double>
                    {
                        ["targetableGcdGapSeconds"] = targetableGap.TotalSeconds,
                        ["previousGcdActionId"] = previous.ActionId,
                        ["nextGcdActionId"] = next.ActionId,
                        ["targetActorId"] = primaryTarget.Id.Value,
                    },
                });
            }
        }
    }

    private static ActorRecord? SelectPrimaryEnemy(AnalyzerContext context, ActorRecord dancer)
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
        var interactionCounts = enemies.ToDictionary(actor => actor.Id, _ => 0);
        foreach (var evt in context.Events.FromActor(dancer.Id))
        {
            if (evt.TargetActorId is not { } targetActorId ||
                !enemyIds.Contains(targetActorId) ||
                evt is not (ActionUseEvent or CastStartEvent or CastEndEvent or DamageEvent))
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

    private static bool ContainsDeath(AnalyzerContext context, ActorId dancerId, TimeRange range)
    {
        return context.Events.ToActor(dancerId)
            .OfType<DeathEvent>()
            .Any(death => death.PullTime >= range.Start && death.PullTime <= range.End);
    }

    private static bool CanUseActionAbsenceAsEvidence(AnalyzerContext context, params NormalizedEvent[] anchors)
    {
        return context.Pull.Provenance.Fidelity == CaptureFidelity.Exact &&
               anchors.Length > 0 &&
               anchors.All(anchor => anchor.Provenance.Fidelity == CaptureFidelity.Exact);
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

    private static CooldownCadenceDefinition Cooldown(string key, string name)
    {
        var action = DancerJobDefinition.Definition.Action(key);
        if (action.Cooldown is not { } cooldown)
        {
            throw new InvalidOperationException($"Dancer action '{key}' does not define a cooldown.");
        }

        return new CooldownCadenceDefinition(key, name, action.ActionId, cooldown);
    }

    private sealed record CooldownCadenceDefinition(
        string Key,
        string Name,
        uint ActionId,
        TimeSpan Cooldown);
}
