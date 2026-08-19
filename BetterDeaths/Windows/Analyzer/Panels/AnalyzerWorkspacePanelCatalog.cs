namespace BetterDeaths.Windows.Analyzer.Panels;

using System.Collections.Generic;

internal static class AnalyzerWorkspacePanelCatalog
{
    public static IReadOnlyList<IAnalyzerWorkspacePanel> CreateDefault()
    {
        return
        [
            new AnalyzerOverviewPanel(),
            new AnalyzerTimelinePanel(),
            new AnalyzerMechanicsPanel(),
            new AnalyzerJobsPanel(),
            new AnalyzerDeathsPanel(),
            new AnalyzerReplayPanel(),
            new AnalyzerSessionPanel(),
        ];
    }
}
