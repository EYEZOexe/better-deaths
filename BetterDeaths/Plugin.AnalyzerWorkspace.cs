namespace BetterDeaths;

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

        analyzerWindow = new AnalyzerWindow(GetCanonicalPullStore(), recapWindow)
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
}
