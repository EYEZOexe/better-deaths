namespace BetterDeaths;

using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using BetterDeaths.Sources.FFLogs;
using BetterDeaths.Windows.Analyzer;

public sealed partial class Plugin
{
    private AnalyzerWindow? analyzerWindow;

    internal void EnsureAnalyzerWorkspaceRegistered()
    {
        if (analyzerWindow is not null)
        {
            return;
        }

        analyzerWindow = new AnalyzerWindow(
            GetCanonicalPullStore(),
            recapWindow,
            CreateFFLogsWorkspaceImportController)
        {
            IsOpen = false,
        };
        windowSystem.AddWindow(analyzerWindow);
    }

    internal void ToggleAnalyzerWorkspace()
    {
        EnsureAnalyzerWorkspaceRegistered();
        analyzerWindow!.IsOpen = !analyzerWindow.IsOpen;
    }

    internal void OpenAnalyzerWorkspace()
    {
        EnsureAnalyzerWorkspaceRegistered();
        analyzerWindow!.IsOpen = true;
    }

    internal void DisposeAnalyzerWorkspace()
    {
        analyzerWindow?.Dispose();
        analyzerWindow = null;
    }

    private AnalyzerWorkspaceFFLogsImportController CreateFFLogsWorkspaceImportController(
        string clientId,
        string clientSecret)
    {
        var session = FFLogsPublicImportSession.Create(
            clientId,
            clientSecret,
            new PullSchemaVersion(CanonicalPullSerializer.CurrentPullSchemaVersion));
        return new AnalyzerWorkspaceFFLogsImportController(
            session,
            GetCanonicalPullStore(),
            session);
    }
}
