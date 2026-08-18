namespace BetterDeaths.Capture.FullPull;

using BetterDeaths.Domain;
using System;
using System.Collections.Generic;

internal sealed class DalamudLiveEventNormalizer
{
    private readonly FullPullRecorder recorder;
    private readonly DateTimeOffset pullStartedAt;
    private readonly string? sourceReference;
    private readonly Dictionary<string, ActorRecord> actorsBySourceKey = new(StringComparer.Ordinal);

    private int nextActorId = 1;
    private long nextSequence = 1;

    public DalamudLiveEventNormalizer(
        FullPullRecorder recorder,
        DateTimeOffset pullStartedAt,
        string? sourceReference = null)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        this.recorder = recorder;
        this.pullStartedAt = pullStartedAt;
        this.sourceReference = sourceReference;
    }

    public void Append(LiveActionEffectFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var sequence = AllocateSequence();
        var sourceActorId = ResolveOptionalActor(fact.Source);
        var targetActorId = ResolveOptionalActor(fact.Target);
        var common = CreateEventEnvelope(fact, sequence, sourceActorId, targetActorId);

        NormalizedEvent evt = fact.Kind switch
        {
            LiveActionEffectKind.Damage => new DamageEvent
            {
                Id = common.Id,
                Sequence = common.Sequence,
                PullTime = common.PullTime,
                ObservedAt = common.ObservedAt,
                SourceActorId = common.SourceActorId,
                TargetActorId = common.TargetActorId,
                Provenance = common.Provenance,
                Amount = fact.Amount,
                ActionId = fact.ActionId,
                IsCritical = fact.IsCritical,
                IsDirectHit = fact.IsDirectHit,
            },
            LiveActionEffectKind.Heal => new HealEvent
            {
                Id = common.Id,
                Sequence = common.Sequence,
                PullTime = common.PullTime,
                ObservedAt = common.ObservedAt,
                SourceActorId = common.SourceActorId,
                TargetActorId = common.TargetActorId,
                Provenance = common.Provenance,
                Amount = fact.Amount,
                ActionId = fact.ActionId,
            },
            LiveActionEffectKind.ActionUse => new ActionUseEvent
            {
                Id = common.Id,
                Sequence = common.Sequence,
                PullTime = common.PullTime,
                ObservedAt = common.ObservedAt,
                SourceActorId = common.SourceActorId,
                TargetActorId = common.TargetActorId,
                Provenance = common.Provenance,
                ActionId = fact.ActionId,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(fact), fact.Kind, "Unsupported live action effect kind."),
        };

        recorder.Append(evt);
    }

    public void Append(LiveStatusFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var sequence = AllocateSequence();
        var common = CreateEventEnvelope(
            fact,
            sequence,
            ResolveOptionalActor(fact.Source),
            ResolveActor(fact.Target));

        NormalizedEvent evt = fact.Applied
            ? new StatusApplyEvent
            {
                Id = common.Id,
                Sequence = common.Sequence,
                PullTime = common.PullTime,
                ObservedAt = common.ObservedAt,
                SourceActorId = common.SourceActorId,
                TargetActorId = common.TargetActorId,
                Provenance = common.Provenance,
                StatusId = fact.StatusId,
                Stacks = fact.Stacks,
                Duration = fact.Duration,
            }
            : new StatusRemoveEvent
            {
                Id = common.Id,
                Sequence = common.Sequence,
                PullTime = common.PullTime,
                ObservedAt = common.ObservedAt,
                SourceActorId = common.SourceActorId,
                TargetActorId = common.TargetActorId,
                Provenance = common.Provenance,
                StatusId = fact.StatusId,
            };

        recorder.Append(evt);
    }

    public void Append(LiveDeathFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var sequence = AllocateSequence();
        var common = CreateEventEnvelope(
            fact,
            sequence,
            ResolveOptionalActor(fact.Source),
            ResolveActor(fact.Target));

        recorder.Append(new DeathEvent
        {
            Id = common.Id,
            Sequence = common.Sequence,
            PullTime = common.PullTime,
            ObservedAt = common.ObservedAt,
            SourceActorId = common.SourceActorId,
            TargetActorId = common.TargetActorId,
            Provenance = common.Provenance,
        });
    }

    public void Append(LiveRaiseFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var sequence = AllocateSequence();
        var common = CreateEventEnvelope(
            fact,
            sequence,
            ResolveOptionalActor(fact.Source),
            ResolveActor(fact.Target));

        recorder.Append(new RaiseEvent
        {
            Id = common.Id,
            Sequence = common.Sequence,
            PullTime = common.PullTime,
            ObservedAt = common.ObservedAt,
            SourceActorId = common.SourceActorId,
            TargetActorId = common.TargetActorId,
            Provenance = common.Provenance,
            ActionId = fact.ActionId,
        });
    }

    public void Append(LiveTargetabilityFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var sequence = AllocateSequence();
        var actorId = ResolveActor(fact.Actor);
        var common = CreateEventEnvelope(fact, sequence, actorId, actorId);

        recorder.Append(new TargetabilityEvent
        {
            Id = common.Id,
            Sequence = common.Sequence,
            PullTime = common.PullTime,
            ObservedAt = common.ObservedAt,
            SourceActorId = common.SourceActorId,
            TargetActorId = common.TargetActorId,
            Provenance = common.Provenance,
            IsTargetable = fact.IsTargetable,
        });
    }

    public void Append(LiveGaugeFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentException.ThrowIfNullOrWhiteSpace(fact.GaugeKey);
        var sequence = AllocateSequence();
        var actorId = ResolveActor(fact.Actor);
        var common = CreateEventEnvelope(fact, sequence, actorId, actorId);

        recorder.Append(new GaugeEvent
        {
            Id = common.Id,
            Sequence = common.Sequence,
            PullTime = common.PullTime,
            ObservedAt = common.ObservedAt,
            SourceActorId = common.SourceActorId,
            TargetActorId = common.TargetActorId,
            Provenance = common.Provenance,
            GaugeKey = fact.GaugeKey,
            Value = fact.Value,
        });
    }

    public void Append(LiveTetherFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var sequence = AllocateSequence();
        var common = CreateEventEnvelope(
            fact,
            sequence,
            ResolveActor(fact.Source),
            ResolveActor(fact.Target));

        recorder.Append(new TetherEvent
        {
            Id = common.Id,
            Sequence = common.Sequence,
            PullTime = common.PullTime,
            ObservedAt = common.ObservedAt,
            SourceActorId = common.SourceActorId,
            TargetActorId = common.TargetActorId,
            Provenance = common.Provenance,
            TetherId = fact.TetherId,
            Active = fact.Active,
        });
    }

    public void Append(LiveMarkerFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var sequence = AllocateSequence();
        var common = CreateEventEnvelope(
            fact,
            sequence,
            sourceActorId: null,
            ResolveActor(fact.Target));

        recorder.Append(new MarkerEvent
        {
            Id = common.Id,
            Sequence = common.Sequence,
            PullTime = common.PullTime,
            ObservedAt = common.ObservedAt,
            SourceActorId = common.SourceActorId,
            TargetActorId = common.TargetActorId,
            Provenance = common.Provenance,
            MarkerId = fact.MarkerId,
            Active = fact.Active,
        });
    }

    public void Append(LiveMechanicSignalFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentException.ThrowIfNullOrWhiteSpace(fact.SignalKey);
        var sequence = AllocateSequence();
        var common = CreateEventEnvelope(
            fact,
            sequence,
            ResolveOptionalActor(fact.Source),
            targetActorId: null);

        recorder.Append(new MechanicSignalEvent
        {
            Id = common.Id,
            Sequence = common.Sequence,
            PullTime = common.PullTime,
            ObservedAt = common.ObservedAt,
            SourceActorId = common.SourceActorId,
            TargetActorId = common.TargetActorId,
            Provenance = common.Provenance,
            SignalKey = fact.SignalKey,
            SignalId = fact.SignalId,
            State = fact.State,
        });
    }

    public void Append(LivePositionFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var sequence = AllocateSequence();
        var actorId = ResolveActor(fact.Actor);

        recorder.Append(new PositionSample
        {
            Sequence = sequence,
            PullTime = CalculatePullTime(fact.ObservedAt),
            ActorId = actorId,
            X = fact.X,
            Y = fact.Y,
            Z = fact.Z,
            Rotation = fact.Rotation,
            Provenance = CreateProvenance(fact),
        });
    }

    public void Append(LiveWorldMarkerFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var sequence = AllocateSequence();

        recorder.Append(new WorldMarkerSample
        {
            Sequence = sequence,
            PullTime = CalculatePullTime(fact.ObservedAt),
            MarkerIndex = fact.MarkerIndex,
            Label = fact.Label,
            Active = fact.Active,
            X = fact.X,
            Y = fact.Y,
            Z = fact.Z,
            Provenance = CreateProvenance(fact),
        });
    }

    private ActorId ResolveActor(LiveActorReference actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor.StableKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor.Name);

        var ownerActorId = actor.Owner is null
            ? (ActorId?)null
            : ResolveActor(actor.Owner);
        var candidate = new ActorRecord
        {
            Id = default,
            Name = actor.Name,
            Kind = actor.Kind,
            ClassJobId = actor.ClassJobId,
            JobAbbreviation = actor.JobAbbreviation,
            OwnerActorId = ownerActorId,
        };

        if (actorsBySourceKey.TryGetValue(actor.StableKey, out var existing))
        {
            if (existing.Name != candidate.Name ||
                existing.Kind != candidate.Kind ||
                existing.ClassJobId != candidate.ClassJobId ||
                existing.JobAbbreviation != candidate.JobAbbreviation ||
                existing.OwnerActorId != candidate.OwnerActorId)
            {
                throw new InvalidOperationException(
                    $"Live actor source key '{actor.StableKey}' was reused with conflicting actor metadata. " +
                    "The source adapter must provide a distinct stable key for a new actor instance.");
            }

            return existing.Id;
        }

        var resolved = candidate with { Id = new ActorId(nextActorId++) };
        actorsBySourceKey.Add(actor.StableKey, resolved);
        recorder.RegisterActor(resolved);
        return resolved.Id;
    }

    private ActorId? ResolveOptionalActor(LiveActorReference? actor)
    {
        return actor is null ? null : ResolveActor(actor);
    }

    private EventEnvelope CreateEventEnvelope(
        LiveCaptureFact fact,
        long sequence,
        ActorId? sourceActorId,
        ActorId? targetActorId)
    {
        return new EventEnvelope(
            new EventId(sequence),
            sequence,
            CalculatePullTime(fact.ObservedAt),
            fact.ObservedAt,
            sourceActorId,
            targetActorId,
            CreateProvenance(fact));
    }

    private EventProvenance CreateProvenance(LiveCaptureFact fact)
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = sourceReference,
            Fidelity = fact.Fidelity,
            Confidence = Math.Clamp(fact.Confidence, 0.0f, 1.0f),
        };
    }

    private TimeSpan CalculatePullTime(DateTimeOffset observedAt)
    {
        return observedAt <= pullStartedAt
            ? TimeSpan.Zero
            : observedAt - pullStartedAt;
    }

    private long AllocateSequence()
    {
        return nextSequence++;
    }

    private readonly record struct EventEnvelope(
        EventId Id,
        long Sequence,
        TimeSpan PullTime,
        DateTimeOffset ObservedAt,
        ActorId? SourceActorId,
        ActorId? TargetActorId,
        EventProvenance Provenance);
}
