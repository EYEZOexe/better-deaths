namespace BetterDeaths.Windows.Analyzer.Panels;

internal interface IAnalyzerWorkspacePanel
{
    string Id { get; }

    string Label { get; }

    void Draw(AnalyzerWorkspacePanelContext context);
}
