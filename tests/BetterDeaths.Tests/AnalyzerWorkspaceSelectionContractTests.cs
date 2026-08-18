namespace BetterDeaths;

using System.Runtime.CompilerServices;

public sealed class AnalyzerWorkspaceSelectionContractTests
{
    [Fact]
    public void SelectionStateOwnsOneSharedPullActorTimeResultAndMechanicContext()
    {
        var source = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/AnalyzerWorkspaceSelection.cs");

        Assert.Contains("public PullId? SelectedPullId", source, StringComparison.Ordinal);
        Assert.Contains("public ActorId? SelectedActorId", source, StringComparison.Ordinal);
        Assert.Contains("public TimeRange? SelectedTimeRange", source, StringComparison.Ordinal);
        Assert.Contains("public AnalysisResultId? SelectedAnalysisResultId", source, StringComparison.Ordinal);
        Assert.Contains("public string? SelectedMechanicOccurrenceId", source, StringComparison.Ordinal);
        Assert.Contains("public long Version", source, StringComparison.Ordinal);
        Assert.Contains("public event Action? Changed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PullSelectionClearsStaleCrossPullContext()
    {
        var source = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/AnalyzerWorkspaceSelection.cs");
        var method = ExtractMethod(source, "public void SelectPull", "public void SelectActor");

        Assert.Contains("selectedPullId = pullId;", method, StringComparison.Ordinal);
        Assert.Contains("selectedActorId = null;", method, StringComparison.Ordinal);
        Assert.Contains("selectedTimeRange = null;", method, StringComparison.Ordinal);
        Assert.Contains("selectedAnalysisResultId = null;", method, StringComparison.Ordinal);
        Assert.Contains("selectedMechanicOccurrenceId = null;", method, StringComparison.Ordinal);
        Assert.Contains("NotifyChanged();", method, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultSelectionSynchronizesActorAndTimeThroughSameState()
    {
        var source = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/AnalyzerWorkspaceSelection.cs");
        var method = ExtractMethod(source, "public void SelectResult", "public void ClearContext");

        Assert.Contains("selectedAnalysisResultId = result.Id;", method, StringComparison.Ordinal);
        Assert.Contains("selectedActorId = actorId;", method, StringComparison.Ordinal);
        Assert.Contains("selectedTimeRange = result.TimeRange;", method, StringComparison.Ordinal);
        Assert.Contains("NotifyChanged();", method, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangeNotificationAdvancesOneSharedVersion()
    {
        var source = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/AnalyzerWorkspaceSelection.cs");
        var method = ExtractMethod(source, "private void NotifyChanged", "\n    }\n}");

        Assert.Contains("Version++;", method, StringComparison.Ordinal);
        Assert.Contains("Changed?.Invoke();", method, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find start marker '{startMarker}'.");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find end marker '{endMarker}'.");
        return source[start..end];
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
