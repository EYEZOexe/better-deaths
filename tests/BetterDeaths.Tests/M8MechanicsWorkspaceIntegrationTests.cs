namespace BetterDeaths;

using BetterDeaths.Analysis.Encounters.DancingMadUltimate;
using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using BetterDeaths.Windows.Analyzer;
using System.Runtime.CompilerServices;

public sealed class M8MechanicsWorkspaceIntegrationTests
{
    private static readonly ActorId Boss = new(99);

    [Fact]
    public async Task DefaultWorkspaceSurfacesForsakenEncounterResultThroughRealEngineComposition()
    {
        var pull = ForsakenPull();
        var controller = AnalyzerWorkspaceDataController.CreateDefault(new SinglePullStore(pull));

        var loaded = await controller.LoadPullAsync(pull.Id);

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Failures);
        var encounterResults = loaded.Results
            .Where(result => result.AnalyzerId == ForsakenOpeningAssignmentAnalyzer.AnalyzerId)
            .ToArray();
        Assert.Equal(4, encounterResults.Length);
        Assert.All(encounterResults, result =>
        {
            Assert.Equal(AnalysisCategory.Mechanic, result.Category);
            Assert.Equal(AnalysisSeverity.Info, result.Severity);
            Assert.Equal(2, result.Actors.Count);
            Assert.NotEmpty(result.Evidence.SelectMany(evidence => evidence.EventIds));
            Assert.NotNull(result.TimeRange);
        });
        Assert.DoesNotContain(loaded.Skipped, skip => skip.AnalyzerId == ForsakenOpeningAssignmentAnalyzer.AnalyzerId);
    }

    [Fact]
    public void MechanicsPanelUsesStructuredEncounterResultsAndSharedSelectionOnly()
    {
        var mechanics = ReadRepositoryFile(
            "BetterDeaths/Windows/Analyzer/Panels/AnalyzerMechanicsPanel.cs");
        var catalog = ReadRepositoryFile(
            "BetterDeaths/Windows/Analyzer/Panels/AnalyzerWorkspacePanelCatalog.cs");
        var recap = ReadRepositoryFile("BetterDeaths/Windows/RecapWindow.cs");

        Assert.Contains("result.Category == AnalysisCategory.Mechanic", mechanics, StringComparison.Ordinal);
        Assert.Contains("context.Selection.SelectResult(result)", mechanics, StringComparison.Ordinal);
        Assert.Contains("result.Severity", mechanics, StringComparison.Ordinal);
        Assert.Contains("result.Title", mechanics, StringComparison.Ordinal);
        Assert.Contains("result.TimeRange", mechanics, StringComparison.Ordinal);
        Assert.Contains("result.Confidence", mechanics, StringComparison.Ordinal);
        Assert.Contains("result.Evidence", mechanics, StringComparison.Ordinal);
        Assert.Contains("new AnalyzerMechanicsPanel()", catalog, StringComparison.Ordinal);

        foreach (var forbidden in new[]
                 {
                     "AnalyzerEngine",
                     "IPullStore",
                     "FFLogs",
                     "Dancer",
                     "Forsaken",
                     "ReplayEncounterModules",
                     "ReplayMarkerSnapshot",
                     "RecapWindow",
                 })
        {
            Assert.DoesNotContain(forbidden, mechanics, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("AnalyzerMechanicsPanel", recap, StringComparison.Ordinal);
    }

    private static RecordedPull ForsakenPull()
    {
        var actors = new[]
        {
            Player(1, "Tank One", "PLD"),
            Player(2, "Tank Two", "WAR"),
            Player(3, "Healer One", "WHM"),
            Player(4, "Healer Two", "SCH"),
            Player(5, "Melee One", "DRG"),
            Player(6, "Melee Two", "VPR"),
            Player(7, "Ranged One", "BRD"),
            Player(8, "Ranged Two", "PCT"),
            new ActorRecord { Id = Boss, Name = "Kefka", Kind = ActorKind.Enemy },
        };
        var statuses = new[]
        {
            (Actor: actors[0].Id, StatusId: 5086u),
            (Actor: actors[1].Id, StatusId: 5084u),
            (Actor: actors[2].Id, StatusId: 5086u),
            (Actor: actors[3].Id, StatusId: 5085u),
            (Actor: actors[4].Id, StatusId: 5085u),
            (Actor: actors[5].Id, StatusId: 5084u),
            (Actor: actors[6].Id, StatusId: 5085u),
            (Actor: actors[7].Id, StatusId: 5086u),
        };
        var provenance = new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = "test:m8-workspace",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };
        var events = statuses.Select((status, index) => (NormalizedEvent)new StatusApplyEvent
        {
            Id = new EventId(index + 1),
            Sequence = index + 1,
            PullTime = TimeSpan.FromSeconds(10 + index * 0.1),
            SourceActorId = Boss,
            TargetActorId = status.Actor,
            Provenance = provenance,
            StatusId = status.StatusId,
            Duration = TimeSpan.FromSeconds(15),
        }).ToArray();

        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1363,
                TerritoryName = "Dancing Mad Ultimate",
                Duration = TimeSpan.FromMinutes(2),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors = actors,
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = "test:m8-workspace",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static ActorRecord Player(int id, string name, string job)
    {
        return new ActorRecord
        {
            Id = new ActorId(id),
            Name = name,
            Kind = ActorKind.Player,
            JobAbbreviation = job,
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

    private sealed class SinglePullStore(RecordedPull pull) : IPullStore
    {
        public Task SaveAsync(RecordedPull savedPull, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RecordedPull?> LoadAsync(PullId id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<RecordedPull?>(pull.Id == id ? pull : null);
        }

        public Task<IReadOnlyList<PullSummary>> QueryAsync(PullQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PullSummary>>([]);

        public Task DeleteAsync(PullId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
