namespace BetterDeaths.Analysis.Generic;

using BetterDeaths.Analysis.Engine;
using System;
using System.Collections.Generic;

internal static class GenericAnalyzerRegistryFactory
{
    public static AnalyzerRegistry CreateDefault()
    {
        return Create(
            Array.Empty<MitigationDefinition>(),
            Array.Empty<GenericTimelineDefinition>());
    }

    public static AnalyzerRegistry Create(
        IReadOnlyList<MitigationDefinition> mitigationDefinitions,
        IReadOnlyList<GenericTimelineDefinition> timelineDefinitions)
    {
        ArgumentNullException.ThrowIfNull(mitigationDefinitions);
        ArgumentNullException.ThrowIfNull(timelineDefinitions);

        var registry = new AnalyzerRegistry();
        registry.Register(new DeathRaiseContextAnalyzer());
        registry.Register(new TargetabilityAwareUptimeAnalyzer());
        registry.Register(new HealingActivityAnalyzer());
        registry.Register(new MitigationCoverageAnalyzer(mitigationDefinitions));
        registry.Register(new GenericTimelineAnalyzer(timelineDefinitions));
        return registry;
    }
}
