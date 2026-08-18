namespace BetterDeaths.Windows.Analyzer;

using BetterDeaths.Domain;
using System;
using System.Collections.Generic;

internal enum AnalyzerWorkspaceNavigationTarget
{
    LegacyDeaths,
    LegacyReplay,
}

internal interface IAnalyzerWorkspaceNavigation
{
    void Request(AnalyzerWorkspaceNavigationTarget target);
}

internal sealed record AnalyzerWorkspacePanelContext
{
    public required AnalyzerWorkspaceSelection Selection { get; init; }

    public RecordedPull? Pull { get; init; }

    public IReadOnlyList<AnalysisResult> Results { get; init; } = Array.Empty<AnalysisResult>();

    public IAnalyzerWorkspaceNavigation? Navigation { get; init; }
}
