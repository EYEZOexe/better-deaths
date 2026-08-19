namespace BetterDeaths.Analysis.Encounters.DancingMadUltimate;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed class ForsakenOpeningAssignmentAnalyzer : IAnalyzerModule
{
    public const string AnalyzerId = "encounter.dmu.forsaken-opening";

    private static readonly TimeSpan OpeningBatchWindow = TimeSpan.FromSeconds(3);

    public string Id => AnalyzerId;

    public AnalyzerScope Scope => AnalyzerScope.Encounter;

    public IReadOnlyCollection<string> Dependencies => Array.Empty<string>();

    public bool Supports(AnalyzerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Pull.Metadata.TerritoryId == ForsakenDefinition.TerritoryId;
    }

    public ValueTask AnalyzeAsync(
        AnalyzerContext context,
        IAnalysisResultSink results,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(results);

        var relevantEvents = context.Events
            .OfType<StatusApplyEvent>()
            .Where(status => ForsakenDefinition.RelevantStatusIds.Contains(status.StatusId))
            .Where(status => status.TargetActorId is not null)
            .OrderBy(status => status.PullTime)
            .ThenBy(status => status.Sequence)
            .ToArray();
        if (relevantEvents.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        var openingStart = relevantEvents[0].PullTime;
        var openingEnd = openingStart + OpeningBatchWindow;
        var openingEvents = relevantEvents
            .Where(status => status.PullTime >= openingStart && status.PullTime <= openingEnd)
            .ToArray();

        var participants = ResolveParticipants(context);
        if (participants is null)
        {
            return ValueTask.CompletedTask;
        }

        var observations = ResolveOpeningObservations(participants, openingEvents);
        if (observations is null)
        {
            return ValueTask.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var layouts = BuildCandidateLayouts(participants, observations);
        var compatible = layouts.Where(layout => layout.IsCompatible).ToArray();
        var range = new TimeRange(
            observations.Values.Min(observation => observation.Event.PullTime),
            observations.Values.Max(observation => observation.Event.PullTime));

        if (compatible.Length == 1)
        {
            AddResolvedLayoutResults(context, compatible[0], observations, range, results);
            return ValueTask.CompletedTask;
        }

        if (compatible.Length > 1)
        {
            AddAmbiguousLayoutResult(context, participants, observations, range, layouts.Length, compatible.Length, results);
            return ValueTask.CompletedTask;
        }

        if (CanUseCompleteAbsenceAsFailureEvidence(context, observations))
        {
            AddIncompatibleLayoutResult(context, participants, observations, range, layouts.Length, results);
        }

        return ValueTask.CompletedTask;
    }

    private static ParticipantSet? ResolveParticipants(AnalyzerContext context)
    {
        var players = context.Pull.Actors
            .Where(actor => actor.Kind == ActorKind.Player)
            .Select(actor => new Participant(actor, EncounterPartyRoleResolver.Resolve(actor)))
            .Where(participant => participant.Role != EncounterPartyRole.Unknown)
            .OrderBy(participant => participant.Actor.Id.Value)
            .ToArray();

        var tanks = players.Where(participant => participant.Role == EncounterPartyRole.Tank).ToArray();
        var healers = players.Where(participant => participant.Role == EncounterPartyRole.Healer).ToArray();
        var melee = players.Where(participant => participant.Role == EncounterPartyRole.Melee).ToArray();
        var ranged = players.Where(participant => participant.Role == EncounterPartyRole.Ranged).ToArray();

        return tanks.Length == 2 && healers.Length == 2 && melee.Length == 2 && ranged.Length == 2
            ? new ParticipantSet(tanks, healers, melee, ranged)
            : null;
    }

    private static IReadOnlyDictionary<ActorId, OpeningObservation>? ResolveOpeningObservations(
        ParticipantSet participants,
        IReadOnlyList<StatusApplyEvent> openingEvents)
    {
        var allParticipants = participants.All.ToDictionary(participant => participant.Actor.Id);
        var observations = new Dictionary<ActorId, OpeningObservation>();

        foreach (var (actorId, participant) in allParticipants)
        {
            var actorEvents = openingEvents
                .Where(status => status.TargetActorId == actorId)
                .OrderBy(status => status.Sequence)
                .ToArray();
            if (actorEvents.Length != 1)
            {
                return null;
            }

            var evt = actorEvents[0];
            var kind = ForsakenDefinition.GetDebuffKind(evt.StatusId);
            if (kind == ForsakenDebuffKind.Unknown)
            {
                return null;
            }

            observations.Add(actorId, new OpeningObservation(participant, kind, evt));
        }

        // Extra relevant status targets in the same opening batch make the captured assignment
        // ambiguous rather than proving a failure for the eight classified party actors.
        if (openingEvents.Any(status => status.TargetActorId is { } target && !allParticipants.ContainsKey(target)))
        {
            return null;
        }

        return observations;
    }

    private static IReadOnlyList<PairingLayout> BuildCandidateLayouts(
        ParticipantSet participants,
        IReadOnlyDictionary<ActorId, OpeningObservation> observations)
    {
        var supportLayouts = BuildTwoByTwoPairings(participants.Tanks, participants.Healers);
        var damageLayouts = BuildTwoByTwoPairings(participants.Melee, participants.Ranged);
        var layouts = new List<PairingLayout>(supportLayouts.Count * damageLayouts.Count);

        foreach (var support in supportLayouts)
        {
            foreach (var damage in damageLayouts)
            {
                var pairs = support.Concat(damage)
                    .Select(pair => ClassifyPair(pair.First, pair.Second, observations))
                    .OrderBy(pair => Math.Min(pair.First.Actor.Id.Value, pair.Second.Actor.Id.Value))
                    .ThenBy(pair => Math.Max(pair.First.Actor.Id.Value, pair.Second.Actor.Id.Value))
                    .ToArray();
                layouts.Add(new PairingLayout(pairs));
            }
        }

        return layouts;
    }

    private static IReadOnlyList<IReadOnlyList<ParticipantPair>> BuildTwoByTwoPairings(
        IReadOnlyList<Participant> firstRole,
        IReadOnlyList<Participant> secondRole)
    {
        return
        [
            [
                new ParticipantPair(firstRole[0], secondRole[0]),
                new ParticipantPair(firstRole[1], secondRole[1]),
            ],
            [
                new ParticipantPair(firstRole[0], secondRole[1]),
                new ParticipantPair(firstRole[1], secondRole[0]),
            ],
        ];
    }

    private static ClassifiedPair ClassifyPair(
        Participant first,
        Participant second,
        IReadOnlyDictionary<ActorId, OpeningObservation> observations)
    {
        var firstObservation = observations[first.Actor.Id];
        var secondObservation = observations[second.Actor.Id];
        return new ClassifiedPair(
            first,
            second,
            ForsakenDefinition.ClassifyOpeningPair(firstObservation.Debuff, secondObservation.Debuff));
    }

    private static void AddResolvedLayoutResults(
        AnalyzerContext context,
        PairingLayout layout,
        IReadOnlyDictionary<ActorId, OpeningObservation> observations,
        TimeRange openingRange,
        IAnalysisResultSink results)
    {
        foreach (var pair in layout.Pairs)
        {
            var firstObservation = observations[pair.First.Actor.Id];
            var secondObservation = observations[pair.Second.Actor.Id];
            var pairEvents = new[] { firstObservation.Event, secondObservation.Event }
                .OrderBy(evt => evt.Sequence)
                .ToArray();
            var pairRange = new TimeRange(pairEvents[0].PullTime, pairEvents[^1].PullTime);
            var groupName = pair.Group == ForsakenPairGroup.GroupA ? "Group A" : "Group B";
            var expectedRule = pair.Group == ForsakenPairGroup.GroupA
                ? "different debuffs with one Stack"
                : "the same debuff on both partners";
            var primaryActor = pair.First.Actor.Id.Value <= pair.Second.Actor.Id.Value
                ? pair.First.Actor.Id
                : pair.Second.Actor.Id;

            results.Add(new AnalysisResult
            {
                Id = StableAnalysisResultIdentity.ForActorWindow(
                    context.Pull.Id,
                    AnalyzerId,
                    primaryActor,
                    pairRange,
                    $"resolved:{Math.Min(pair.First.Actor.Id.Value, pair.Second.Actor.Id.Value)}:{Math.Max(pair.First.Actor.Id.Value, pair.Second.Actor.Id.Value)}:{pair.Group}"),
                AnalyzerId = AnalyzerId,
                Severity = AnalysisSeverity.Info,
                Category = AnalysisCategory.Mechanic,
                Title = $"Forsaken {groupName}: {pair.First.Actor.Name} ↔ {pair.Second.Actor.Name}",
                Summary =
                    $"Expected: the Kroxy-Rinon opening rule classifies {groupName} by {expectedRule}. " +
                    $"Observed: {pair.First.Actor.Name} has {firstObservation.Debuff} and {pair.Second.Actor.Name} has {secondObservation.Debuff}. " +
                    "Cause: this is the only complete Tank↔Healer / Melee↔Ranged pairing layout compatible with all eight observed opening debuffs. " +
                    "Consequence: the pair can use this Group A/B assignment for the subsequent tower instructions; this result does not claim those towers were resolved correctly.",
                TimeRange = pairRange,
                Actors = [pair.First.Actor.Id, pair.Second.Actor.Id],
                Evidence =
                [
                    new AnalysisEvidence
                    {
                        EventIds = pairEvents.Select(evt => evt.Id).ToArray(),
                        ActorIds = [pair.First.Actor.Id, pair.Second.Actor.Id],
                        TimeRange = pairRange,
                        Explanation =
                            "The two canonical Forsaken status applications provide the observed debuff kinds for the uniquely resolved role-compatible partner pair.",
                    },
                ],
                Confidence = EvidenceConfidence(pairEvents),
                Metrics = new Dictionary<string, double>
                {
                    ["groupA"] = pair.Group == ForsakenPairGroup.GroupA ? 1 : 0,
                    ["groupB"] = pair.Group == ForsakenPairGroup.GroupB ? 1 : 0,
                    ["firstStatusId"] = firstObservation.Event.StatusId,
                    ["secondStatusId"] = secondObservation.Event.StatusId,
                    ["openingBatchSeconds"] = (openingRange.End - openingRange.Start).TotalSeconds,
                },
            });
        }
    }

    private static void AddAmbiguousLayoutResult(
        AnalyzerContext context,
        ParticipantSet participants,
        IReadOnlyDictionary<ActorId, OpeningObservation> observations,
        TimeRange range,
        int candidateLayoutCount,
        int compatibleLayoutCount,
        IAnalysisResultSink results)
    {
        var allActors = participants.All.Select(participant => participant.Actor.Id).OrderBy(id => id.Value).ToArray();
        var allEvents = observations.Values.Select(observation => observation.Event).OrderBy(evt => evt.Sequence).ToArray();
        results.Add(new AnalysisResult
        {
            Id = StableAnalysisResultIdentity.ForActorWindow(
                context.Pull.Id,
                AnalyzerId,
                allActors[0],
                range,
                $"ambiguous:{compatibleLayoutCount}"),
            AnalyzerId = AnalyzerId,
            Severity = AnalysisSeverity.Info,
            Category = AnalysisCategory.Mechanic,
            Title = "Forsaken opening partner assignment remains ambiguous",
            Summary =
                $"Expected: Kroxy-Rinon pairs Tanks↔Healers and Melee↔Ranged, then classifies each pair as Group A (different with Stack) or Group B (same). " +
                $"Observed: the eight opening debuffs admit {compatibleLayoutCount} of {candidateLayoutCount} role-compatible full-party layouts. " +
                "Cause: canonical actors do not contain static slot labels and the debuff pattern does not uniquely identify one pairing. " +
                "Consequence: no single partner/group layout is presented as fact; later assignment checks must remain unknown until stronger evidence exists.",
            TimeRange = range,
            Actors = allActors,
            Evidence =
            [
                new AnalysisEvidence
                {
                    EventIds = allEvents.Select(evt => evt.Id).ToArray(),
                    ActorIds = allActors,
                    TimeRange = range,
                    Explanation = "All eight observed Forsaken status applications support more than one complete role-compatible pairing layout.",
                },
            ],
            Confidence = EvidenceConfidence(allEvents),
            Metrics = new Dictionary<string, double>
            {
                ["candidateLayoutCount"] = candidateLayoutCount,
                ["compatibleLayoutCount"] = compatibleLayoutCount,
                ["assignmentUnique"] = 0,
            },
        });
    }

    private static void AddIncompatibleLayoutResult(
        AnalyzerContext context,
        ParticipantSet participants,
        IReadOnlyDictionary<ActorId, OpeningObservation> observations,
        TimeRange range,
        int candidateLayoutCount,
        IAnalysisResultSink results)
    {
        var allActors = participants.All.Select(participant => participant.Actor.Id).OrderBy(id => id.Value).ToArray();
        var allEvents = observations.Values.Select(observation => observation.Event).OrderBy(evt => evt.Sequence).ToArray();
        var observed = string.Join(", ", observations.Values
            .OrderBy(observation => observation.Participant.Actor.Id.Value)
            .Select(observation => $"{observation.Participant.Actor.Name}={observation.Debuff}"));

        results.Add(new AnalysisResult
        {
            Id = StableAnalysisResultIdentity.ForActorWindow(
                context.Pull.Id,
                AnalyzerId,
                allActors[0],
                range,
                "incompatible-opening-layout"),
            AnalyzerId = AnalyzerId,
            Severity = AnalysisSeverity.Warning,
            Category = AnalysisCategory.Mechanic,
            Title = "Forsaken opening debuffs do not admit a Kroxy-Rinon partner layout",
            Summary =
                "Expected: all four Tank↔Healer / Melee↔Ranged partner pairs must be classifiable as Group A (different with one Stack) or Group B (same debuff). " +
                $"Observed: {observed}. " +
                $"Cause: none of the {candidateLayoutCount} complete role-compatible pairing layouts makes every pair compatible with the opening Group A/B rule. " +
                "Consequence: a deterministic Kroxy-Rinon Group A/B partner assignment cannot be established from this exact opening evidence, so subsequent tower assignments derived from that pairing are undefined. This is a strategy-compatibility finding, not automatic player blame.",
            TimeRange = range,
            Actors = allActors,
            Evidence =
            [
                new AnalysisEvidence
                {
                    EventIds = allEvents.Select(evt => evt.Id).ToArray(),
                    ActorIds = allActors,
                    TimeRange = range,
                    Explanation =
                        "The complete exact opening status batch provides one Stack/Spread/Cone observation for each classified party member; all possible role-compatible layouts were evaluated and none satisfied every pair rule.",
                },
            ],
            Confidence = EvidenceConfidence(allEvents),
            Metrics = new Dictionary<string, double>
            {
                ["candidateLayoutCount"] = candidateLayoutCount,
                ["compatibleLayoutCount"] = 0,
                ["completeParticipantCount"] = allActors.Length,
                ["exactFailureEvidence"] = 1,
            },
        });
    }

    private static bool CanUseCompleteAbsenceAsFailureEvidence(
        AnalyzerContext context,
        IReadOnlyDictionary<ActorId, OpeningObservation> observations)
    {
        return context.Pull.Provenance.Fidelity == CaptureFidelity.Exact &&
               observations.Count == 8 &&
               observations.Values.All(observation => observation.Event.Provenance.Fidelity == CaptureFidelity.Exact);
    }

    private static float EvidenceConfidence(IEnumerable<StatusApplyEvent> events)
    {
        var values = events.Select(evt => Math.Clamp(evt.Provenance.Confidence, 0.0f, 1.0f)).ToArray();
        return values.Length == 0 ? 0.0f : values.Min();
    }

    private sealed record Participant(ActorRecord Actor, EncounterPartyRole Role);

    private sealed record ParticipantPair(Participant First, Participant Second);

    private sealed record OpeningObservation(
        Participant Participant,
        ForsakenDebuffKind Debuff,
        StatusApplyEvent Event);

    private sealed record ClassifiedPair(
        Participant First,
        Participant Second,
        ForsakenPairGroup Group);

    private sealed record PairingLayout(IReadOnlyList<ClassifiedPair> Pairs)
    {
        public bool IsCompatible => Pairs.All(pair => pair.Group is ForsakenPairGroup.GroupA or ForsakenPairGroup.GroupB);
    }

    private sealed record ParticipantSet(
        IReadOnlyList<Participant> Tanks,
        IReadOnlyList<Participant> Healers,
        IReadOnlyList<Participant> Melee,
        IReadOnlyList<Participant> Ranged)
    {
        public IReadOnlyList<Participant> All => Tanks.Concat(Healers).Concat(Melee).Concat(Ranged).ToArray();
    }
}
