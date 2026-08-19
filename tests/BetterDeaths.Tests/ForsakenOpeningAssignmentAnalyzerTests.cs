namespace BetterDeaths;

using BetterDeaths.Analysis.Encounters.DancingMadUltimate;
using BetterDeaths.Analysis.Engine;
using BetterDeaths.Domain;
using System.Runtime.CompilerServices;

public sealed class ForsakenOpeningAssignmentAnalyzerTests
{
    private static readonly ActorId Tank1 = new(1);
    private static readonly ActorId Tank2 = new(2);
    private static readonly ActorId Healer1 = new(3);
    private static readonly ActorId Healer2 = new(4);
    private static readonly ActorId Melee1 = new(5);
    private static readonly ActorId Melee2 = new(6);
    private static readonly ActorId Ranged1 = new(7);
    private static readonly ActorId Ranged2 = new(8);
    private static readonly ActorId Boss = new(9);

    [Fact]
    public void AnalyzerUsesEncounterScope()
    {
        Assert.Equal(AnalyzerScope.Encounter, new ForsakenOpeningAssignmentAnalyzer().Scope);
    }

    [Fact]
    public async Task UniqueCompatibleOpeningProducesFourEvidenceBackedPairAssignments()
    {
        var pull = Pull(
            PullDataSourceKind.ImportedFile,
            Statuses(
                (Tank1, 5086),
                (Tank2, 5084),
                (Healer1, 5086),
                (Healer2, 5085),
                (Melee1, 5085),
                (Melee2, 5084),
                (Ranged1, 5085),
                (Ranged2, 5086)));

        var run = await Analyze(pull);

        Assert.Empty(run.Failures);
        Assert.Empty(run.Skipped);
        Assert.Equal(4, run.Results.Count);
        Assert.All(run.Results, result =>
        {
            Assert.Equal(AnalysisSeverity.Info, result.Severity);
            Assert.Equal(AnalysisCategory.Mechanic, result.Category);
            Assert.Equal(2, result.Actors.Count);
            Assert.Equal(2, Assert.Single(result.Evidence).EventIds.Count);
            Assert.NotNull(result.TimeRange);
            Assert.Contains("Expected:", result.Summary, StringComparison.Ordinal);
            Assert.Contains("Observed:", result.Summary, StringComparison.Ordinal);
            Assert.Contains("Cause:", result.Summary, StringComparison.Ordinal);
            Assert.Contains("Consequence:", result.Summary, StringComparison.Ordinal);
        });

        Assert.Contains(run.Results, result =>
            result.Title.Contains("Group B", StringComparison.Ordinal) &&
            result.Actors.Contains(Tank1) && result.Actors.Contains(Healer1));
        Assert.Contains(run.Results, result =>
            result.Title.Contains("Group A", StringComparison.Ordinal) &&
            result.Actors.Contains(Tank2) && result.Actors.Contains(Healer2));
        Assert.Contains(run.Results, result =>
            result.Title.Contains("Group B", StringComparison.Ordinal) &&
            result.Actors.Contains(Melee1) && result.Actors.Contains(Ranged1));
        Assert.Contains(run.Results, result =>
            result.Title.Contains("Group A", StringComparison.Ordinal) &&
            result.Actors.Contains(Melee2) && result.Actors.Contains(Ranged2));
    }

    [Fact]
    public async Task CompleteExactIncompatibleOpeningProducesOneStrategyCompatibilityWarning()
    {
        var pull = Pull(
            PullDataSourceKind.ImportedFile,
            Statuses(
                (Tank1, 5086),
                (Tank2, 5086),
                (Healer1, 5085),
                (Healer2, 5085),
                (Melee1, 5085),
                (Melee2, 5084),
                (Ranged1, 5085),
                (Ranged2, 5086)));

        var result = Assert.Single((await Analyze(pull)).Results);

        Assert.Equal(AnalysisSeverity.Warning, result.Severity);
        Assert.Equal(AnalysisCategory.Mechanic, result.Category);
        Assert.Contains("do not admit a Kroxy-Rinon partner layout", result.Title, StringComparison.Ordinal);
        Assert.Equal(8, result.Actors.Count);
        Assert.Equal(8, Assert.Single(result.Evidence).EventIds.Count);
        Assert.Equal(4d, result.Metrics["candidateLayoutCount"]);
        Assert.Equal(0d, result.Metrics["compatibleLayoutCount"]);
        Assert.Equal(1d, result.Metrics["exactFailureEvidence"]);
        Assert.Contains("Expected:", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Observed:", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Cause:", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Consequence:", result.Summary, StringComparison.Ordinal);
        Assert.Contains("not automatic player blame", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MultipleCompatibleLayoutsRemainNeutralAndDoNotInventStaticSlots()
    {
        var pull = Pull(
            PullDataSourceKind.ImportedFile,
            Statuses(
                (Tank1, 5084),
                (Tank2, 5084),
                (Healer1, 5084),
                (Healer2, 5084),
                (Melee1, 5084),
                (Melee2, 5084),
                (Ranged1, 5084),
                (Ranged2, 5084)));

        var result = Assert.Single((await Analyze(pull)).Results);

        Assert.Equal(AnalysisSeverity.Info, result.Severity);
        Assert.Contains("remains ambiguous", result.Title, StringComparison.Ordinal);
        Assert.Equal(4d, result.Metrics["compatibleLayoutCount"]);
        Assert.Equal(0d, result.Metrics["assignmentUnique"]);
        Assert.Contains("does not contain static slot labels", result.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("MT", result.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("H1", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IncompleteOpeningEvidenceDoesNotBecomeFailure()
    {
        var events = Statuses(
            (Tank1, 5086),
            (Tank2, 5086),
            (Healer1, 5085),
            (Healer2, 5085),
            (Melee1, 5085),
            (Melee2, 5084),
            (Ranged1, 5085));

        var run = await Analyze(Pull(PullDataSourceKind.ImportedFile, events));

        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task SampledIncompatibleOpeningEvidenceDoesNotBecomeFailure()
    {
        var events = Statuses(
            (Tank1, 5086),
            (Tank2, 5086),
            (Healer1, 5085),
            (Healer2, 5085),
            (Melee1, 5085),
            (Melee2, 5084),
            (Ranged1, 5085),
            (Ranged2, 5086));
        events[0] = events[0] with
        {
            Provenance = Provenance(PullDataSourceKind.ImportedFile, CaptureFidelity.Sampled),
        };

        var run = await Analyze(Pull(PullDataSourceKind.ImportedFile, events));

        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task DuplicateRelevantStatusForOneActorMakesOpeningEvidenceAmbiguousInsteadOfFailure()
    {
        var events = Statuses(
            (Tank1, 5086),
            (Tank2, 5086),
            (Healer1, 5085),
            (Healer2, 5085),
            (Melee1, 5085),
            (Melee2, 5084),
            (Ranged1, 5085),
            (Ranged2, 5086)).ToList();
        events.Add(Status(20, 11.5, Tank1, 5084, PullDataSourceKind.ImportedFile));

        var run = await Analyze(Pull(PullDataSourceKind.ImportedFile, events.ToArray()));

        Assert.Empty(run.Results);
    }

    [Fact]
    public async Task EquivalentLocalAndFFLogsFactsProduceEquivalentEncounterSemantics()
    {
        var definitions = new[]
        {
            (Tank1, 5086u), (Tank2, 5086u), (Healer1, 5085u), (Healer2, 5085u),
            (Melee1, 5085u), (Melee2, 5084u), (Ranged1, 5085u), (Ranged2, 5086u),
        };
        var local = Pull(PullDataSourceKind.DalamudLive, Statuses(PullDataSourceKind.DalamudLive, definitions));
        var imported = Pull(PullDataSourceKind.FFLogs, Statuses(PullDataSourceKind.FFLogs, definitions));

        var localRun = await Analyze(local);
        var importedRun = await Analyze(imported);

        Assert.Equal(Project(localRun.Results), Project(importedRun.Results));
    }

    [Fact]
    public async Task WrongTerritoryIsSkipped()
    {
        var pull = Pull(
            PullDataSourceKind.ImportedFile,
            Statuses((Tank1, 5084), (Tank2, 5084), (Healer1, 5084), (Healer2, 5084),
                (Melee1, 5084), (Melee2, 5084), (Ranged1, 5084), (Ranged2, 5084))) with
        {
            Metadata = new PullMetadata
            {
                TerritoryId = 999,
                TerritoryName = "Other",
                Duration = TimeSpan.FromSeconds(60),
            },
        };

        var run = await Analyze(pull);

        Assert.Empty(run.Results);
        Assert.Equal(ForsakenOpeningAssignmentAnalyzer.AnalyzerId, Assert.Single(run.Skipped).AnalyzerId);
    }

    [Fact]
    public void AnalyzerBoundaryHasNoSourceUiReplayNetworkOrPersistenceDependencies()
    {
        var source = ReadRepositoryFile(
            "BetterDeaths/Analysis/Encounters/DancingMadUltimate/ForsakenOpeningAssignmentAnalyzer.cs");

        Assert.DoesNotContain("FFLogs", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dalamud", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ImGui", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IPullStore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayEncounterModules", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayMarker", source, StringComparison.Ordinal);
    }

    private static async Task<AnalyzerRunResult> Analyze(RecordedPull pull)
    {
        var registry = new AnalyzerRegistry();
        registry.Register(new ForsakenOpeningAssignmentAnalyzer());
        return await new AnalyzerEngine(registry).AnalyzeAsync(pull);
    }

    private static RecordedPull Pull(PullDataSourceKind sourceKind, params StatusApplyEvent[] events)
    {
        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("88888888-9999-aaaa-bbbb-cccccccccccc")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1363,
                TerritoryName = "Dancing Mad Ultimate",
                Duration = TimeSpan.FromSeconds(60),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                Player(Tank1, "Tank One", "PLD"),
                Player(Tank2, "Tank Two", "WAR"),
                Player(Healer1, "Healer One", "WHM"),
                Player(Healer2, "Healer Two", "SCH"),
                Player(Melee1, "Melee One", "DRG"),
                Player(Melee2, "Melee Two", "VPR"),
                Player(Ranged1, "Ranged One", "DNC"),
                Player(Ranged2, "Ranged Two", "PCT"),
                new ActorRecord { Id = Boss, Name = "Kefka", Kind = ActorKind.Enemy },
            ],
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = sourceKind,
                SourceReference = "test:forsaken-opening",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static ActorRecord Player(ActorId id, string name, string job)
    {
        return new ActorRecord
        {
            Id = id,
            Name = name,
            Kind = ActorKind.Player,
            JobAbbreviation = job,
        };
    }

    private static StatusApplyEvent[] Statuses(params (ActorId Actor, uint StatusId)[] statuses)
    {
        return Statuses(PullDataSourceKind.ImportedFile, statuses);
    }

    private static StatusApplyEvent[] Statuses(
        PullDataSourceKind sourceKind,
        params (ActorId Actor, uint StatusId)[] statuses)
    {
        return statuses.Select((entry, index) =>
            Status(index + 1, 10.0 + index * 0.1, entry.Actor, entry.StatusId, sourceKind)).ToArray();
    }

    private static StatusApplyEvent Status(
        long sequence,
        double seconds,
        ActorId target,
        uint statusId,
        PullDataSourceKind sourceKind)
    {
        return new StatusApplyEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = Boss,
            TargetActorId = target,
            Provenance = Provenance(sourceKind),
            StatusId = statusId,
            Duration = TimeSpan.FromSeconds(15),
        };
    }

    private static EventProvenance Provenance(
        PullDataSourceKind sourceKind,
        CaptureFidelity fidelity = CaptureFidelity.Exact)
    {
        return new EventProvenance
        {
            SourceKind = sourceKind,
            SourceReference = "test:forsaken-opening",
            Fidelity = fidelity,
            Confidence = 1.0f,
        };
    }

    private static IReadOnlyList<ResultProjection> Project(IReadOnlyList<AnalysisResult> results)
    {
        return results.Select(result => new ResultProjection(
            result.AnalyzerId,
            result.Severity,
            result.Category,
            result.Title,
            result.Summary,
            result.TimeRange,
            string.Join(",", result.Actors.Select(actor => actor.Value)),
            result.Confidence,
            string.Join(",", result.Metrics.OrderBy(metric => metric.Key).Select(metric => $"{metric.Key}={metric.Value:R}")),
            string.Join(",", result.Evidence.SelectMany(evidence => evidence.EventIds).Select(id => id.Value))))
            .ToArray();
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

    private sealed record ResultProjection(
        string AnalyzerId,
        AnalysisSeverity Severity,
        AnalysisCategory Category,
        string Title,
        string Summary,
        TimeRange? TimeRange,
        string Actors,
        float Confidence,
        string Metrics,
        string EvidenceIds);
}
