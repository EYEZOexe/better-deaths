namespace BetterDeaths.Windows.Analyzer;

using BetterDeaths.Analysis.Sessions;
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

internal interface IAnalyzerSessionNavigation
{
    void OpenEvidence(SessionEvidenceReference evidence);
}

internal sealed record AnalyzerWorkspacePanelContext
{
    public required AnalyzerWorkspaceSelection Selection { get; init; }

    public RecordedPull? Pull { get; init; }

    public IReadOnlyList<AnalysisResult> Results { get; init; } = Array.Empty<AnalysisResult>();

    // Derived once when the selected pull changes by the outer workspace. Shell panels
    // should not rescan an entire full-pull event stream every render frame.
    public IReadOnlyList<DeathEvent> DeathEvents { get; init; } = Array.Empty<DeathEvent>();

    public AnalyzerSessionLoaded? Session { get; init; }

    public IAnalyzerWorkspaceNavigation? Navigation { get; init; }

    public IAnalyzerSessionNavigation? SessionNavigation { get; init; }
}
