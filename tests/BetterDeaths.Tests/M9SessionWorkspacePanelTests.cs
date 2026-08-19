namespace BetterDeaths;

using BetterDeaths.Analysis.Sessions;
using BetterDeaths.Domain;
using BetterDeaths.Windows.Analyzer;
using System.Runtime.CompilerServices;

public sealed class M9SessionWorkspacePanelTests
{
    [Fact]
    public void SessionPanelConsumesStructuredOutputsAndKeepsAnalysisPersistenceAndEncounterLogicOutOfRendering()
    {
        var panel = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/Panels/AnalyzerSessionPanel.cs");
        var catalog = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/Panels/AnalyzerWorkspacePanelCatalog.cs");
        var recap = ReadRepositoryFile("BetterDeaths/Windows/RecapWindow.cs");

        Assert.Contains("loaded.Analysis", panel, StringComparison.Ordinal);
        Assert.Contains("progression.Phases", panel, StringComparison.Ordinal);
        Assert.Contains("analysis.Recurrences", panel, StringComparison.Ordinal);
        Assert.Contains("FindingCount", panel, StringComparison.Ordinal);
        Assert.Contains("OpportunityCount", panel, StringComparison.Ordinal);
        Assert.Contains("UnknownCount", panel, StringComparison.Ordinal);
        Assert.Contains("wipeCauses.Causes", panel, StringComparison.Ordinal);
        Assert.Contains("analysis.Trends", panel, StringComparison.Ordinal);
        Assert.Contains("navigation.OpenEvidence(evidence)", panel, StringComparison.Ordinal);
        Assert.Contains("MaxVisibleRecurrences", panel, StringComparison.Ordinal);
        Assert.Contains("new AnalyzerSessionPanel()", catalog, StringComparison.Ordinal);

        foreach (var forbidden in new[]
                 {
                     "SessionIntelligenceAnalyzer.Analyze",
                     "IPullStore",
                     "PullQuery",
                     "FFLogs",
                     "Dalamud.Plugin",
                     "Forsaken",
                     "Dancer",
                     "ReplayEncounterModules",
                     "RecapWindow",
                 })
        {
            Assert.DoesNotContain(forbidden, panel, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("AnalyzerSessionPanel", recap, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowLoadsSessionsAsynchronouslyAndDrillsEvidenceBackIntoSharedPullResultSelection()
    {
        var window = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/AnalyzerWindow.cs");

        Assert.Contains("AnalyzerSessionDataController.CreateDefault", window, StringComparison.Ordinal);
        Assert.Contains("Task.Run(async () =>", window, StringComparison.Ordinal);
        Assert.Contains("await sessionController.LoadAsync", window, StringComparison.Ordinal);
        Assert.Contains("SessionQueryLimit", window, StringComparison.Ordinal);
        Assert.Contains("Session = snapshot.LoadedSession", window, StringComparison.Ordinal);
        Assert.Contains("SessionNavigation = this", window, StringComparison.Ordinal);
        Assert.Contains("selection.SelectPull(evidence.PullId)", window, StringComparison.Ordinal);
        Assert.Contains("QueuePullLoad(evidence.PullId, evidence.ResultId)", window, StringComparison.Ordinal);
        Assert.Contains("loaded.Results.FirstOrDefault(item => item.Id == resultId)", window, StringComparison.Ordinal);
        Assert.Contains("selection.SelectResult(result)", window, StringComparison.Ordinal);
        Assert.Contains("sessionController.InvalidatePendingLoad", window, StringComparison.Ordinal);

        Assert.DoesNotContain(".GetAwaiter().GetResult()", window, StringComparison.Ordinal);
        Assert.DoesNotContain(".Result;", window, StringComparison.Ordinal);
        Assert.DoesNotContain(".Wait()", window, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionNavigationContractCarriesExplicitPullResultEvidenceRatherThanUiText()
    {
        var contextSource = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/AnalyzerWorkspacePanelContext.cs");
        Assert.Contains("void OpenEvidence(SessionEvidenceReference evidence)", contextSource, StringComparison.Ordinal);

        var navigation = new RecordingSessionNavigation();
        var evidence = new SessionEvidenceReference
        {
            PullId = new PullId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            ResultId = new AnalysisResultId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            FindingKey = new SessionFindingKey("test.analyzer", "test.rule"),
            PullLocalActorIds = [new ActorId(7)],
            TimeRange = new TimeRange(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(11)),
        };

        navigation.OpenEvidence(evidence);

        Assert.Same(evidence, navigation.LastEvidence);
        Assert.Equal("test.analyzer", navigation.LastEvidence?.FindingKey.AnalyzerId);
        Assert.Equal("test.rule", navigation.LastEvidence?.FindingKey.RuleKey);
    }

    private sealed class RecordingSessionNavigation : IAnalyzerSessionNavigation
    {
        public SessionEvidenceReference? LastEvidence { get; private set; }

        public void OpenEvidence(SessionEvidenceReference evidence)
        {
            LastEvidence = evidence;
        }
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
