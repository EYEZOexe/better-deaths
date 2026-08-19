namespace BetterDeaths.Analysis.Jobs;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

internal sealed record JobActionDefinition
{
    public required string Key { get; init; }

    public required uint ActionId { get; init; }

    public required bool IsGcd { get; init; }

    public TimeSpan? Cooldown { get; init; }

    public int Charges { get; init; } = 1;
}

internal sealed record JobStatusDefinition
{
    public required string Key { get; init; }

    public required uint StatusId { get; init; }

    public TimeSpan? Duration { get; init; }
}

internal sealed class JobDefinition
{
    private readonly IReadOnlyDictionary<string, JobActionDefinition> actionsByKey;
    private readonly IReadOnlyDictionary<string, JobStatusDefinition> statusesByKey;

    public JobDefinition(
        string key,
        string displayName,
        string jobAbbreviation,
        IEnumerable<JobActionDefinition> actions,
        IEnumerable<JobStatusDefinition> statuses)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(jobAbbreviation);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(statuses);

        Key = key.Trim();
        DisplayName = displayName.Trim();
        JobAbbreviation = jobAbbreviation.Trim().ToUpperInvariant();

        var actionArray = actions.ToArray();
        var statusArray = statuses.ToArray();
        ValidateActions(actionArray);
        ValidateStatuses(statusArray);

        actionsByKey = new ReadOnlyDictionary<string, JobActionDefinition>(
            actionArray.ToDictionary(action => action.Key, StringComparer.Ordinal));
        statusesByKey = new ReadOnlyDictionary<string, JobStatusDefinition>(
            statusArray.ToDictionary(status => status.Key, StringComparer.Ordinal));
        Actions = Array.AsReadOnly(actionArray);
        Statuses = Array.AsReadOnly(statusArray);
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string JobAbbreviation { get; }

    public IReadOnlyList<JobActionDefinition> Actions { get; }

    public IReadOnlyList<JobStatusDefinition> Statuses { get; }

    public JobActionDefinition Action(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return actionsByKey.TryGetValue(key, out var action)
            ? action
            : throw new KeyNotFoundException($"Job definition '{Key}' does not contain action '{key}'.");
    }

    public JobStatusDefinition Status(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return statusesByKey.TryGetValue(key, out var status)
            ? status
            : throw new KeyNotFoundException($"Job definition '{Key}' does not contain status '{key}'.");
    }

    private static void ValidateActions(IReadOnlyList<JobActionDefinition> actions)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<uint>();
        foreach (var action in actions)
        {
            ArgumentNullException.ThrowIfNull(action);
            ArgumentException.ThrowIfNullOrWhiteSpace(action.Key);
            if (action.ActionId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actions), "Job action IDs must be non-zero.");
            }

            if (!keys.Add(action.Key))
            {
                throw new ArgumentException($"Duplicate job action key '{action.Key}'.", nameof(actions));
            }

            if (!ids.Add(action.ActionId))
            {
                throw new ArgumentException($"Duplicate job action ID '{action.ActionId}'.", nameof(actions));
            }

            if (action.Cooldown is { } cooldown && cooldown <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(actions), $"Cooldown for '{action.Key}' must be positive.");
            }

            if (action.Charges <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actions), $"Charges for '{action.Key}' must be positive.");
            }
        }
    }

    private static void ValidateStatuses(IReadOnlyList<JobStatusDefinition> statuses)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<uint>();
        foreach (var status in statuses)
        {
            ArgumentNullException.ThrowIfNull(status);
            ArgumentException.ThrowIfNullOrWhiteSpace(status.Key);
            if (status.StatusId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(statuses), "Job status IDs must be non-zero.");
            }

            if (!keys.Add(status.Key))
            {
                throw new ArgumentException($"Duplicate job status key '{status.Key}'.", nameof(statuses));
            }

            if (!ids.Add(status.StatusId))
            {
                throw new ArgumentException($"Duplicate job status ID '{status.StatusId}'.", nameof(statuses));
            }

            if (status.Duration is { } duration && duration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(statuses), $"Duration for '{status.Key}' must be positive.");
            }
        }
    }
}
