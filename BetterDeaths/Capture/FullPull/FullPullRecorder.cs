namespace BetterDeaths.Capture.FullPull;

using BetterDeaths.Domain;
using System;
using System.Collections.Generic;

internal sealed record PullStartContext
{
    public required PullId PullId { get; init; }

    public required PullMetadata Metadata { get; init; }

    public required PullSchemaVersion SchemaVersion { get; init; }

    public required PullProvenance Provenance { get; init; }

    public required bool DutyActive { get; init; }
}

internal readonly record struct PullEndContext(TimeSpan Duration);

internal sealed class FullPullRecorder
{
    private readonly List<ActorRecord> actors = [];
    private readonly Dictionary<ActorId, ActorRecord> actorsById = [];
    private readonly List<NormalizedEvent> events = [];
    private readonly List<PositionSample> positions = [];
    private readonly List<WorldMarkerSample> worldMarkers = [];

    private PullStartContext? startContext;
    private bool combatObserved;
    private long lastSequence;

    public bool IsActive => startContext is not null;

    public int EventCount => events.Count;

    public int PositionCount => positions.Count;

    public int WorldMarkerCount => worldMarkers.Count;

    public void Begin(PullStartContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (IsActive)
        {
            throw new InvalidOperationException("A full pull is already being recorded.");
        }

        Reset();
        startContext = context;
    }

    public void MarkCombatObserved()
    {
        EnsureActive();
        combatObserved = true;
    }

    public void RegisterActor(ActorRecord actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        EnsureActive();

        if (actorsById.TryGetValue(actor.Id, out var existing))
        {
            if (existing != actor)
            {
                throw new InvalidOperationException($"Actor ID {actor.Id.Value} was registered with conflicting canonical data.");
            }

            return;
        }

        actorsById.Add(actor.Id, actor);
        actors.Add(actor);
    }

    public void Append(NormalizedEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        EnsureActive();
        AcceptSequence(evt.Sequence);
        events.Add(evt);
    }

    public void Append(PositionSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        EnsureActive();
        AcceptSequence(sample.Sequence);
        positions.Add(sample);
    }

    public void Append(WorldMarkerSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        EnsureActive();
        AcceptSequence(sample.Sequence);
        worldMarkers.Add(sample);
    }

    public bool HasMeaningfulCombatData(TimeSpan duration)
    {
        if (startContext is null)
        {
            return false;
        }

        return PullFinalizationPolicy.IsMeaningful(new PullFinalizationFacts(
            startContext.DutyActive,
            combatObserved,
            events.Count,
            duration));
    }

    public bool TryFinalize(PullEndContext context, out RecordedPull? pull)
    {
        EnsureActive();

        if (!HasMeaningfulCombatData(context.Duration))
        {
            pull = null;
            Reset();
            return false;
        }

        var started = startContext!;
        pull = new RecordedPull
        {
            Id = started.PullId,
            Metadata = started.Metadata with { Duration = context.Duration },
            SchemaVersion = started.SchemaVersion,
            Actors = actors.ToArray(),
            Events = events.ToArray(),
            Positions = positions.ToArray(),
            WorldMarkers = worldMarkers.ToArray(),
            Provenance = started.Provenance,
        };

        Reset();
        return true;
    }

    public void Reset()
    {
        startContext = null;
        combatObserved = false;
        lastSequence = 0;
        actors.Clear();
        actorsById.Clear();
        events.Clear();
        positions.Clear();
        worldMarkers.Clear();
    }

    private void AcceptSequence(long sequence)
    {
        if (sequence <= lastSequence)
        {
            throw new InvalidOperationException(
                $"Canonical sequence must increase strictly within a pull. Last={lastSequence}, received={sequence}.");
        }

        lastSequence = sequence;
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("No full pull is currently being recorded.");
        }
    }
}
