namespace BetterDeaths;

using System.Runtime.CompilerServices;

public sealed class AnalyzerWindowIntegrationContractTests
{
    [Fact]
    public void AnalyzerWindowLoadsAndAnalyzesOffTheRenderPath()
    {
        var source = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/AnalyzerWindow.cs");

        Assert.Contains("Task.Run", source, StringComparison.Ordinal);
        Assert.Contains("QueryPullsAsync(PullQueryLimit)", source, StringComparison.Ordinal);
        Assert.Contains("LoadPullAsync(pullId, requestCts.Token)", source, StringComparison.Ordinal);
        Assert.Contains("AnalyzerWorkspacePanelCatalog.CreateDefault", source, StringComparison.Ordinal);
        Assert.Contains("DeathEvents = loaded.DeathEvents", source, StringComparison.Ordinal);

        Assert.DoesNotContain(".Result", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Wait(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAwaiter().GetResult", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FileCanonicalPullStore", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginRegistersAnalyzerWindowThroughExistingWindowSystemBoundary()
    {
        var pluginIntegration = ReadRepositoryFile("BetterDeaths/Plugin.AnalyzerWorkspace.cs");
        var widget = ReadRepositoryFile("BetterDeaths/Windows/CurrentPullWidgetWindow.cs");

        Assert.Contains("new AnalyzerWindow(GetCanonicalPullStore(), recapWindow)", pluginIntegration, StringComparison.Ordinal);
        Assert.Contains("windowSystem.AddWindow(analyzerWindow)", pluginIntegration, StringComparison.Ordinal);
        Assert.Contains("EnsureAnalyzerWorkspaceRegistered", widget, StringComparison.Ordinal);
        Assert.Contains("Analyzer Workspace", widget, StringComparison.Ordinal);
        Assert.Contains("plugin.ToggleAnalyzerWorkspace()", widget, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyzerWorkspaceRemainsOutsideMonolithicRecapWindow()
    {
        var recap = ReadRepositoryFile("BetterDeaths/Windows/RecapWindow.cs");
        var analyzer = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/AnalyzerWindow.cs");

        Assert.DoesNotContain("AnalyzerWindow", recap, StringComparison.Ordinal);
        Assert.DoesNotContain("AnalyzerWorkspaceSelection", recap, StringComparison.Ordinal);
        Assert.DoesNotContain("AnalyzerWorkspaceDataController", recap, StringComparison.Ordinal);

        Assert.Contains("RecapWindow", analyzer, StringComparison.Ordinal);
        Assert.Contains("FocusLatestPull", analyzer, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenReplayForDeath", analyzer, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkspaceDataControllerIsPureAndUsesStoreAndEngineContracts()
    {
        var source = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/AnalyzerWorkspaceDataController.cs");

        Assert.Contains("IPullStore", source, StringComparison.Ordinal);
        Assert.Contains("AnalyzerEngine", source, StringComparison.Ordinal);
        Assert.Contains("pull.Events.OfType<DeathEvent>().ToArray()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ImGui", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dalamud", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FileCanonicalPullStore", source, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(
        string relativePath,
        [CallerFilePath] string testSourcePath = "")
    {
        var testDirectory = Path.GetDirectoryName(testSourcePath)
            ?? throw new InvalidOperationException("Could not resolve test source directory.");
        var repositoryRoot = Path.GetFullPath(Path.Combine(testDirectory, "..", ".."));
        return File.ReadAllText(Path.Combine(
            repositoryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
