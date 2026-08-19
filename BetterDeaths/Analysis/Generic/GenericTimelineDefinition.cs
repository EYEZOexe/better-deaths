namespace BetterDeaths.Analysis.Generic;

using System;

internal enum GenericTimelineKind
{
    CooldownAction,
    BuffStatus,
}

internal sealed record GenericTimelineDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required GenericTimelineKind Kind { get; init; }

    public required uint ReferenceId { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (ReferenceId == 0)
        {
            throw new InvalidOperationException($"Timeline definition '{Id}' must reference a non-zero action/status ID.");
        }
    }
}
