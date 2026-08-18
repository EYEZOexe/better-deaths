namespace BetterDeaths.Analysis.Index;

using BetterDeaths.Domain;
using System;
using System.Collections.Generic;

internal sealed class ActorIndex
{
    private readonly IReadOnlyDictionary<ActorId, ActorRecord> actorsById;

    public ActorIndex(IReadOnlyList<ActorRecord> actors)
    {
        ArgumentNullException.ThrowIfNull(actors);

        var indexed = new Dictionary<ActorId, ActorRecord>(actors.Count);
        foreach (var actor in actors)
        {
            ArgumentNullException.ThrowIfNull(actor);
            if (!indexed.TryAdd(actor.Id, actor))
            {
                throw new InvalidOperationException($"Duplicate canonical actor ID {actor.Id.Value}.");
            }
        }

        actorsById = indexed;
    }

    public int Count => actorsById.Count;

    public bool TryGet(ActorId id, out ActorRecord? actor)
    {
        return actorsById.TryGetValue(id, out actor);
    }

    public ActorRecord GetRequired(ActorId id)
    {
        return actorsById.TryGetValue(id, out var actor)
            ? actor
            : throw new KeyNotFoundException($"Canonical actor ID {id.Value} is not present in the pull.");
    }
}
