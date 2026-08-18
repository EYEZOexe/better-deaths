namespace BetterDeaths.Analysis.Engine;

using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class AnalyzerRegistry
{
    private readonly Dictionary<string, IAnalyzerModule> modulesById = new(StringComparer.Ordinal);

    public IReadOnlyList<IAnalyzerModule> Modules => modulesById.Values
        .OrderBy(module => module.Id, StringComparer.Ordinal)
        .ToArray();

    public void Register(IAnalyzerModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(module.Id);

        if (!modulesById.TryAdd(module.Id, module))
        {
            throw new InvalidOperationException($"Analyzer module '{module.Id}' is already registered.");
        }
    }

    public bool TryGet(string analyzerId, out IAnalyzerModule? module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analyzerId);
        return modulesById.TryGetValue(analyzerId, out module);
    }
}
