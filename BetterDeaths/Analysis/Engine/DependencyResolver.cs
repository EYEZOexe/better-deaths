namespace BetterDeaths.Analysis.Engine;

using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class AnalyzerDependencyException : InvalidOperationException
{
    public AnalyzerDependencyException(string message)
        : base(message)
    {
    }
}

internal static class DependencyResolver
{
    public static IReadOnlyList<IAnalyzerModule> Resolve(IReadOnlyList<IAnalyzerModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var modulesById = new Dictionary<string, IAnalyzerModule>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            ArgumentNullException.ThrowIfNull(module);
            ArgumentException.ThrowIfNullOrWhiteSpace(module.Id);
            if (!modulesById.TryAdd(module.Id, module))
            {
                throw new AnalyzerDependencyException($"Duplicate analyzer module ID '{module.Id}'.");
            }
        }

        foreach (var module in modulesById.Values)
        {
            foreach (var dependency in NormalizeDependencies(module))
            {
                if (dependency == module.Id)
                {
                    throw new AnalyzerDependencyException($"Analyzer '{module.Id}' cannot depend on itself.");
                }

                if (!modulesById.ContainsKey(dependency))
                {
                    throw new AnalyzerDependencyException(
                        $"Analyzer '{module.Id}' declares missing dependency '{dependency}'.");
                }
            }
        }

        var remainingDependencies = modulesById.ToDictionary(
            pair => pair.Key,
            pair => new HashSet<string>(NormalizeDependencies(pair.Value), StringComparer.Ordinal),
            StringComparer.Ordinal);
        var dependents = modulesById.Keys.ToDictionary(
            id => id,
            _ => new List<string>(),
            StringComparer.Ordinal);

        foreach (var pair in remainingDependencies)
        {
            foreach (var dependency in pair.Value)
            {
                dependents[dependency].Add(pair.Key);
            }
        }

        var ready = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var pair in remainingDependencies)
        {
            if (pair.Value.Count == 0)
            {
                ready.Add(pair.Key);
            }
        }

        var ordered = new List<IAnalyzerModule>(modulesById.Count);
        while (ready.Count > 0)
        {
            var nextId = ready.Min!;
            ready.Remove(nextId);
            ordered.Add(modulesById[nextId]);

            foreach (var dependentId in dependents[nextId].OrderBy(id => id, StringComparer.Ordinal))
            {
                var dependencySet = remainingDependencies[dependentId];
                dependencySet.Remove(nextId);
                if (dependencySet.Count == 0)
                {
                    ready.Add(dependentId);
                }
            }

            remainingDependencies.Remove(nextId);
        }

        if (ordered.Count != modulesById.Count)
        {
            var cycleIds = remainingDependencies.Keys.OrderBy(id => id, StringComparer.Ordinal);
            throw new AnalyzerDependencyException(
                $"Analyzer dependency cycle detected among: {string.Join(", ", cycleIds)}.");
        }

        return ordered;
    }

    internal static IReadOnlyList<string> NormalizeDependencies(IAnalyzerModule module)
    {
        return module.Dependencies
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(dependency => dependency, StringComparer.Ordinal)
            .ToArray();
    }
}
