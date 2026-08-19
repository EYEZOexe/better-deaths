namespace BetterDeaths;

using BetterDeaths.Domain;
using BetterDeaths.Windows.Analyzer;
using BetterDeaths.Windows.Analyzer.Panels;
using System.Runtime.CompilerServices;

public sealed class AnalyzerWorkspacePanelTests
{
    [Fact]
    public void ResultSelectionSynchronizesResultActorAndTimeInOneSharedState()
    {
        var selection = new AnalyzerWorkspaceSelection();
        var changedCount = 0;
        selection.Changed += () => changedCount++;
        var result = Result(1, actorId: 7, startSeconds: 12, endSeconds: 14);

        selection.SelectResult(result);

        Assert.Equal(result.Id, selection.SelectedAnalysisResultId);
        Assert.Equal(new ActorId(7), selection.SelectedActorId);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(14)), selection.SelectedTimeRange);
        Assert.Equal(1, selection.Version);
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void ChangingPullClearsStaleSharedPanelContext()
    {
        var selection = new AnalyzerWorkspaceSelection();
        selection.SelectPull(new PullId(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        selection.SelectResult(Result(2, actorId: 4, startSeconds: 20, endSeconds: 21));
        selection.SelectMechanicOccurrence(" mechanic-1 ");

        selection.SelectPull(new PullId(Guid.Parse("22222222-2222-2222-2222-222222222222")));

        Assert.Equal(new PullId(Guid.Parse("22222222-2222-2222-2222-222222222222")), selection.SelectedPullId);
        Assert.Null(selection.SelectedActorId);
        Assert.Null(selection.SelectedTimeRange);
        Assert.Null(selection.SelectedAnalysisResultId);
        Assert.Null(selection.SelectedMechanicOccurrenceId);
    }

    [Fact]
    public void PanelContractsReceiveTheSameSelectionInstanceRatherThanCallingEachOther()
    {
        var selection = new AnalyzerWorkspaceSelection();
        var context = new AnalyzerWorkspacePanelContext { Selection = selection };
        var timeline = new CapturingPanel("timeline");
        var jobs = new CapturingPanel("jobs");
        var deaths = new CapturingPanel("deaths");
        var replay = new CapturingPanel("replay");

        timeline.Draw(context);
        jobs.Draw(context);
        deaths.Draw(context);
        replay.Draw(context);

        Assert.Same(selection, timeline.SeenSelection);
        Assert.Same(selection, jobs.SeenSelection);
        Assert.Same(selection, deaths.SeenSelection);
        Assert.Same(selection, replay.SeenSelection);
    }

    [Fact]
    public void NavigationContractCarriesLegacyDeathAndReplayIntentWithoutPanelCoupling()
    {
        var navigation = new RecordingNavigation();
        var context = new AnalyzerWorkspacePanelContext
        {
            Selection = new AnalyzerWorkspaceSelection(),
            Navigation = navigation,
        };

        context.Navigation!.Request(AnalyzerWorkspaceNavigationTarget.LegacyDeaths);
        context.Navigation.Request(AnalyzerWorkspaceNavigationTarget.LegacyReplay);

        Assert.Equal(
            new[]
            {
                AnalyzerWorkspaceNavigationTarget.LegacyDeaths,
                AnalyzerWorkspaceNavigationTarget.LegacyReplay,
            },
            navigation.Requests);
    }

    [Fact]
    public void ShellRenderersConsumeSharedContextAndDoNotGrowLegacyRecapWindow()
    {
        var overview = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/Panels/AnalyzerOverviewPanel.cs");
        var timeline = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/Panels/AnalyzerTimelinePanel.cs");
        var jobs = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/Panels/AnalyzerJobsPanel.cs");
        var deaths = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/Panels/AnalyzerDeathsPanel.cs");
        var replay = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/Panels/AnalyzerReplayPanel.cs");
        var catalog = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/Panels/AnalyzerWorkspacePanelCatalog.cs");
        var recap = ReadRepositoryFile("BetterDeaths/Windows/RecapWindow.cs");

        Assert.Contains("context.Selection", overview, StringComparison.Ordinal);
        Assert.Contains("context.Selection", timeline, StringComparison.Ordinal);
        Assert.Contains("context.Selection", jobs, StringComparison.Ordinal);
        Assert.Contains("context.Selection", deaths, StringComparison.Ordinal);
        Assert.Contains("context.Selection", replay, StringComparison.Ordinal);
        Assert.Contains("AnalysisCategory.Job", jobs, StringComparison.Ordinal);
        Assert.Contains("context.Selection.SelectResult(result)", jobs, StringComparison.Ordinal);
        Assert.Contains("new AnalyzerJobsPanel()", catalog, StringComparison.Ordinal);
        Assert.Contains("context.DeathEvents", deaths, StringComparison.Ordinal);
        Assert.DoesNotContain("pull.Events.OfType", deaths, StringComparison.Ordinal);
        Assert.Contains("AnalyzerWorkspaceNavigationTarget.LegacyDeaths", deaths, StringComparison.Ordinal);
        Assert.Contains("AnalyzerWorkspaceNavigationTarget.LegacyReplay", replay, StringComparison.Ordinal);

        foreach (var source in new[] { overview, timeline, jobs, deaths, replay })
        {
            Assert.DoesNotContain("AnalyzerEngine", source, StringComparison.Ordinal);
            Assert.DoesNotContain("IPullStore", source, StringComparison.Ordinal);
            Assert.DoesNotContain("RecapWindow", source, StringComparison.Ordinal);
            Assert.DoesNotContain("FFLogs", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Dancer", source, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("AnalyzerWorkspaceSelection", recap, StringComparison.Ordinal);
        Assert.DoesNotContain("AnalyzerOverviewPanel", recap, StringComparison.Ordinal);
        Assert.DoesNotContain("AnalyzerTimelinePanel", recap, StringComparison.Ordinal);
        Assert.DoesNotContain("AnalyzerJobsPanel", recap, StringComparison.Ordinal);
    }

    [Fact]
    public void JobsPanelUsesStructuredJobResultsAndPrimaryActorConventionGenerically()
    {
        var jobs = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/Panels/AnalyzerJobsPanel.cs");

        Assert.Contains("result.Category == AnalysisCategory.Job", jobs, StringComparison.Ordinal);
        Assert.Contains("result.Actors[0]", jobs, StringComparison.Ordinal);
        Assert.Contains("result.Evidence", jobs, StringComparison.Ordinal);
        Assert.Contains("result.Confidence", jobs, StringComparison.Ordinal);
        Assert.DoesNotContain("job.dnc", jobs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DancerJobDefinition", jobs, StringComparison.Ordinal);
        Assert.DoesNotContain("DancerCoreExecutionAnalyzer", jobs, StringComparison.Ordinal);
        Assert.DoesNotContain("DancerBurstAndUptimeAnalyzer", jobs, StringComparison.Ordinal);
    }

    private static AnalysisResult Result(int id, int actorId, double startSeconds, double endSeconds)
    {
        return new AnalysisResult
        {
            Id = new AnalysisResultId(Guid.Parse($"00000000-0000-0000-0000-{id:D12}")),
            AnalyzerId = "test.workspace",
            Severity = AnalysisSeverity.Info,
            Category = AnalysisCategory.DataQuality,
            Title = "Workspace test",
            Summary = "Workspace test result",
            TimeRange = new TimeRange(TimeSpan.FromSeconds(startSeconds), TimeSpan.FromSeconds(endSeconds)),
            Actors = [new ActorId(actorId)],
            Evidence = [],
        };
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

    private sealed class CapturingPanel(string id) : IAnalyzerWorkspacePanel
    {
        public string Id => id;

        public string Label => id;

        public AnalyzerWorkspaceSelection? SeenSelection { get; private set; }

        public void Draw(AnalyzerWorkspacePanelContext context)
        {
            SeenSelection = context.Selection;
        }
    }

    private sealed class RecordingNavigation : IAnalyzerWorkspaceNavigation
    {
        public List<AnalyzerWorkspaceNavigationTarget> Requests { get; } = [];

        public void Request(AnalyzerWorkspaceNavigationTarget target)
        {
            Requests.Add(target);
        }
    }
}
