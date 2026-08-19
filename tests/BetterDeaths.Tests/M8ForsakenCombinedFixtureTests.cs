namespace BetterDeaths;

using BetterDeaths.Analysis.Encounters.DancingMadUltimate;
using BetterDeaths.Analysis.Engine;
using BetterDeaths.Domain;

public sealed class M8ForsakenCombinedFixtureTests
{
    private static readonly ActorId Tank1 = new(1);
    private static readonly ActorId Tank2 = new(2);
    private static readonly ActorId Healer1 = new(3);
    private static readonly ActorId Healer2 = new(4);
    private static readonly ActorId Melee1 = new(5);
    private static readonly ActorId Melee2 = new(6);
    private static readonly ActorId Ranged1 = new(7);
    private static readonly ActorId Ranged2 = new(8);
    private static readonly ActorId Boss = new(99);

    [Fact]
    public async Task GoldenCompatibleForsakenOpeningProducesEvidenceBackedAssignments()
    {
        var run = await Analyze(Pull(
            PullDataSourceKind.DalamudLive,
            CaptureFidelity.Exact,
            (Tank1, 5086),
            (Tank2, 5084),
            (Healer1, 5086),
            (Healer2, 5085),
            (Melee1, 5085),
            (Melee2, 5084),
            (Ranged1, 5085),
            (Ranged2, 5086)));

        Assert.Empty(run.Failures);
        Assert.Empty(run.Skipped);
        Assert.Equal(4, run.Results.Count);
        Assert.All(run.Results, result =>
        {
            Assert.Equal(ForsakenOpeningAssignmentAnalyzer.AnalyzerId, result.AnalyzerId);
            Assert.Equal(AnalysisSeverity.Info, result.Severity);
            Assert.Equal(AnalysisCategory.Mechanic, result.Category);
            Assert.Equal(2, result.Actors.Count);
            Assert.NotNull(result.TimeRange);
            Assert.InRange(result.Confidence, 0.0f, 1.0f);
            var evidence = Assert.Single(result.Evidence);
            Assert.Equal(2, evidence.EventIds.Count);
            Assert.Equal(2, evidence.ActorIds.Count);
            Assert.NotNull(evidence.TimeRange);
            Assert.Contains("Expected:", result.Summary, StringComparison.Ordinal);
            Assert.Contains("Observed:", result.Summary, StringComparison.Ordinal);
            Assert.Contains("Cause:", result.Summary, StringComparison.Ordinal);
            Assert.Contains("Consequence:", result.Summary, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task GoldenIncompatibleExactOpeningProducesOneNonBlamingWarningWithCompleteEvidence()
    {
        var run = await Analyze(Pull(
            PullDataSourceKind.DalamudLive,
            CaptureFidelity.Exact,
            (Tank1, 5086),
            (Tank2, 5086),
            (Healer1, 5085),
            (Healer2, 5085),
            (Melee1, 5085),
            (Melee2, 5084),
            (Ranged1, 5085),
            (Ranged2, 5086)));

        Assert.Empty(run.Failures);
        Assert.Empty(run.Skipped);
        var result = Assert.Single(run.Results);
        Assert.Equal(AnalysisSeverity.Warning, result.Severity);
        Assert.Equal(AnalysisCategory.Mechanic, result.Category);
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
    public async Task AmbiguousIncompleteAndSampledEvidenceNeverBecomeActionableFailures()
    {
        var ambiguous = await Analyze(Pull(
            PullDataSourceKind.DalamudLive,
            CaptureFidelity.Exact,
            (Tank1, 5084), (Tank2, 5084),
            (Healer1, 5084), (Healer2, 5084),
            (Melee1, 5084), (Melee2, 5084),
            (Ranged1, 5084), (Ranged2, 5084)));
        var ambiguousResult = Assert.Single(ambiguous.Results);
        Assert.Equal(AnalysisSeverity.Info, ambiguousResult.Severity);
        Assert.Equal(4d, ambiguousResult.Metrics["compatibleLayoutCount"]);
        Assert.Equal(0d, ambiguousResult.Metrics["assignmentUnique"]);

        var incomplete = await Analyze(Pull(
            PullDataSourceKind.DalamudLive,
            CaptureFidelity.Exact,
            (Tank1, 5086), (Tank2, 5086),
            (Healer1, 5085), (Healer2, 5085),
            (Melee1, 5085), (Melee2, 5084),
            (Ranged1, 5085)));
        Assert.Empty(incomplete.Results);

        var sampled = await Analyze(Pull(
            PullDataSourceKind.DalamudLive,
            CaptureFidelity.Sampled,
            (Tank1, 5086), (Tank2, 5086),
            (Healer1, 5085), (Healer2, 5085),
            (Melee1, 5085), (Melee2, 5084),
            (Ranged1, 5085), (Ranged2, 5086)));
        Assert.DoesNotContain(sampled.Results, result => result.Severity >= AnalysisSeverity.Warning);
    }

    [Fact]
    public async Task EquivalentLocalAndFFLogsCanonicalFactsKeepTheSameEncounterMeaning()
    {
        var facts = new[]
        {
            (Tank1, 5086u), (Tank2, 5086u),
            (Healer1, 5085u), (Healer2, 5085u),
            (Melee1, 5085u), (Melee2, 5084u),
            (Ranged1, 5085u), (Ranged2, 5086u),
        };
        var local = await Analyze(Pull(PullDataSourceKind.DalamudLive, CaptureFidelity.Exact, facts));
        var imported = await Analyze(Pull(PullDataSourceKind.FFLogs, CaptureFidelity.Exact, facts));

        Assert.Equal(Project(local.Results), Project(imported.Results));
    }

    private static async Task<AnalyzerRunResult> Analyze(RecordedPull pull)
    {
        var registry = new AnalyzerRegistry();
        registry.Register(new ForsakenOpeningAssignmentAnalyzer());
        return await new AnalyzerEngine(registry).AnalyzeAsync(pull);
    }

    private static RecordedPull Pull(
        PullDataSourceKind sourceKind,
        CaptureFidelity fidelity,
        params (ActorId Actor, uint StatusId)[] statuses)
    {
        var provenance = new EventProvenance
        {
            SourceKind = sourceKind,
            SourceReference = "fixture:m8-forsaken-signoff",
            Fidelity = fidelity,
            Confidence = fidelity == CaptureFidelity.Exact ? 1.0f : 0.7f,
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
            Id = new PullId(Guid.Parse("12345678-90ab-cdef-1234-567890abcdef")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1363,
                TerritoryName = "Dancing Mad Ultimate",
                Duration = TimeSpan.FromMinutes(2),
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
                Player(Ranged1, "Ranged One", "BRD"),
                Player(Ranged2, "Ranged Two", "PCT"),
                new ActorRecord { Id = Boss, Name = "Kefka", Kind = ActorKind.Enemy },
            ],
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = sourceKind,
                SourceReference = "fixture:m8-forsaken-signoff",
                Fidelity = fidelity,
                Confidence = fidelity == CaptureFidelity.Exact ? 1.0f : 0.7f,
            },
        };
    }

    private static ActorRecord Player(ActorId id, string name, string job) => new()
    {
        Id = id,
        Name = name,
        Kind = ActorKind.Player,
        JobAbbreviation = job,
    };

    private static IReadOnlyList<ResultProjection> Project(IReadOnlyList<AnalysisResult> results) =>
        results.Select(result => new ResultProjection(
            result.AnalyzerId,
            result.Severity,
            result.Category,
            result.Title,
            result.Summary,
            result.TimeRange,
            string.Join(",", result.Actors.Select(actor => actor.Value)),
            result.Confidence,
            string.Join(",", result.Metrics.OrderBy(metric => metric.Key).Select(metric => $"{metric.Key}={metric.Value:R}"))))
        .ToArray();

    private sealed record ResultProjection(
        string AnalyzerId,
        AnalysisSeverity Severity,
        AnalysisCategory Category,
        string Title,
        string Summary,
        TimeRange? TimeRange,
        string Actors,
        float Confidence,
        string Metrics);
}
