namespace BetterDeaths.Windows.Analyzer;

using BetterDeaths.Analysis.Encounters.DancingMadUltimate;
using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Generic;
using BetterDeaths.Analysis.Jobs.Dancer;

internal static class AnalyzerWorkspaceEngineComposition
{
    public static AnalyzerEngine CreateDefault()
    {
        var registry = new AnalyzerRegistry();

        // Generic, job and encounter modules all compose through the same engine seam and consume
        // canonical pull/index contracts. Rendering/session orchestration stays downstream of results.
        registry.Register(new DeathRaiseContextAnalyzer());
        registry.Register(new HealingActivityAnalyzer());
        registry.Register(new TargetabilityAwareUptimeAnalyzer());
        registry.Register(new DancerCoreExecutionAnalyzer());
        registry.Register(new DancerBurstAndUptimeAnalyzer());
        registry.Register(new ForsakenOpeningAssignmentAnalyzer());

        return new AnalyzerEngine(registry);
    }
}
