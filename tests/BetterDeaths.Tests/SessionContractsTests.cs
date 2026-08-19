namespace BetterDeaths;

using BetterDeaths.Analysis.Encounters.DancingMadUltimate;
using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Sessions;
using BetterDeaths.Domain;
using System.Runtime.CompilerServices;

public sealed class SessionContractsTests
{
    private static readonly ActorId Boss = new(99);

    [Fact]
    public void FindingIdentityUsesAnalyzerAndRuleKeyInsteadOfDisplayProseOrResultIdentity()
    {
        var first = Result(
            "encounter.example",
            "mechanic.assignment.failed",
            "English display title",
            "English summary");
        var localized = first with
        {
            Id = AnalysisResultId.New(),
            Title = "Título traducido",
            Summary = "Texto de presentación diferente",
        };

        Assert.True(SessionFindingKey.TryCreate(first, out var firstKey));
        Assert.True(SessionFindingKey.TryCreate(localized, out var localizedKey));
        Assert.Equal(firstKey, localizedKey);
        Assert.Equal("encounter.example:mechanic.assignment.failed", firstKey.ToString());
    }

    [Fact]
    public void FindingIdentityChangesWhenAnalyzerOrRuleChangesAndRejectsMissingRuleKey()
    {
        var baseline = Result("job.dnc", "burst.devilment-late", "A", "A");
        var differentRule = baseline with { RuleKey = "burst.flourish-drift" };
        var differentAnalyzer = baseline with { AnalyzerId = "job.other" };
        var missingRule = baseline with { RuleKey = null };

        Assert.True(SessionFindingKey.TryCreate(baseline, out var baselineKey));
        Assert.True(SessionFindingKey.TryCreate(differentRule, out var differentRuleKey));
        Assert.True(SessionFindingKey.TryCreate(differentAnalyzer, out var differentAnalyzerKey));
        Assert.NotEqual(baselineKey, differentRuleKey);
        Assert.NotEqual(baselineKey, differentAnalyzerKey);
        Assert.False(SessionFindingKey.TryCreate(missingRule, out _));
        Assert.Throws<ArgumentException>(() => new SessionFindingKey(" ", "rule"));
        Assert.Throws<ArgumentException>(() => new SessionFindingKey("analyzer", " "));
    }

    [Fact]
    public void CrossPullEvidenceKeepsPullAndResultIdentityExplicit()
    {
        var resultId = new AnalysisResultId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var key = new SessionFindingKey("encounter.dmu.forsaken-opening", "opening-assignment.incompatible");
        var first = new SessionEvidenceReference
        {
            PullId = new PullId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            ResultId = resultId,
            FindingKey = key,
            PullLocalActorIds = [new ActorId(1)],
            TimeRange = new TimeRange(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(11)),
        };
        var second = first with
        {
            PullId = new PullId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
        };

        Assert.NotEqual(first.PullId, second.PullId);
        Assert.Equal(first.ResultId, second.ResultId);
        Assert.Equal(first.FindingKey, second.FindingKey);
    }

    [Fact]
    public void OccurrenceCountsExposeRateAndUnknownCountWithoutInventingDenominators()
    {
        var counts = new SessionOccurrenceCounts(findingCount: 4, opportunityCount: 11, unknownCount: 3);

        Assert.Equal(4, counts.FindingCount);
        Assert.Equal(11, counts.OpportunityCount);
        Assert.Equal(3, counts.UnknownCount);
        Assert.NotNull(counts.Rate);
        Assert.Equal(4d / 11d, counts.Rate!.Value, 8);
        Assert.Null(new SessionOccurrenceCounts(0, 0, 5).Rate);
        Assert.Throws<ArgumentException>(() => new SessionOccurrenceCounts(2, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOccurrenceCounts(-1, 1, 0));
    }

    [Fact]
    public void SessionIdentityAndParticipantKeysRejectEmptyValues()
    {
        Assert.Throws<ArgumentException>(() => new RaidSessionId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new SessionParticipantKey(" "));

        var sessionId = new RaidSessionId(Guid.Parse("12345678-1234-1234-1234-123456789abc"));
        var participant = new SessionParticipantKey("  player@example  ");

        Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789abc"), sessionId.Value);
        Assert.Equal("player@example", participant.Value);
    }

    [Fact]
    public async Task ForsakenActionableResultExposesStableRuleKeyForSessionRecurrence()
    {
        var registry = new AnalyzerRegistry();
        registry.Register(new ForsakenOpeningAssignmentAnalyzer());
        var run = await new AnalyzerEngine(registry).AnalyzeAsync(ForsakenIncompatiblePull());

        var result = Assert.Single(run.Results);
        Assert.Equal(AnalysisSeverity.Warning, result.Severity);
        Assert.Equal(ForsakenOpeningAssignmentAnalyzer.IncompatibleAssignmentRuleKey, result.RuleKey);
        Assert.True(SessionFindingKey.TryCreate(result, out var key));
        Assert.Equal(ForsakenOpeningAssignmentAnalyzer.AnalyzerId, key.AnalyzerId);
        Assert.Equal(ForsakenOpeningAssignmentAnalyzer.IncompatibleAssignmentRuleKey, key.RuleKey);
    }

    [Fact]
    public void SessionContractsHaveNoSourceUiPersistenceOrSinglePullEngineDependency()
    {
        var source = ReadRepositoryFile("BetterDeaths/Analysis/Sessions/SessionContracts.cs");

        foreach (var forbidden in new[]
                 {
                     "IPullStore",
                     "PullSummary",
                     "FFLogs",
                     "Dalamud",
                     "ImGui",
                     "HttpClient",
                     "AnalyzerContext",
                     "AnalyzerEngine",
                     "RecapWindow",
                 })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }

        Assert.Contains("PullId PullId", source, StringComparison.Ordinal);
        Assert.Contains("AnalysisResultId ResultId", source, StringComparison.Ordinal);
        Assert.Contains("AnalyzerId", source, StringComparison.Ordinal);
        Assert.Contains("RuleKey", source, StringComparison.Ordinal);
    }

    private static AnalysisResult Result(string analyzerId, string? ruleKey, string title, string summary) => new()
    {
        Id = AnalysisResultId.New(),
        AnalyzerId = analyzerId,
        RuleKey = ruleKey,
        Severity = AnalysisSeverity.Warning,
        Category = AnalysisCategory.Mechanic,
        Title = title,
        Summary = summary,
        Evidence = [],
    };

    private static RecordedPull ForsakenIncompatiblePull()
    {
        var tanks = new[] { Player(1, "Tank One", "PLD"), Player(2, "Tank Two", "WAR") };
        var healers = new[] { Player(3, "Healer One", "WHM"), Player(4, "Healer Two", "SCH") };
        var melee = new[] { Player(5, "Melee One", "DRG"), Player(6, "Melee Two", "VPR") };
        var ranged = new[] { Player(7, "Ranged One", "BRD"), Player(8, "Ranged Two", "PCT") };
        var assignments = new[]
        {
            (Actor: tanks[0].Id, StatusId: 5086u),
            (Actor: tanks[1].Id, StatusId: 5086u),
            (Actor: healers[0].Id, StatusId: 5085u),
            (Actor: healers[1].Id, StatusId: 5085u),
            (Actor: melee[0].Id, StatusId: 5085u),
            (Actor: melee[1].Id, StatusId: 5084u),
            (Actor: ranged[0].Id, StatusId: 5085u),
            (Actor: ranged[1].Id, StatusId: 5086u),
        };
        var provenance = new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = "test:m9-session-contracts",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };
        var events = assignments.Select((entry, index) => (NormalizedEvent)new StatusApplyEvent
        {
            Id = new EventId(index + 1),
            Sequence = index + 1,
            PullTime = TimeSpan.FromSeconds(10 + index * 0.1),
            SourceActorId = Boss,
            TargetActorId = entry.Actor,
            Provenance = provenance,
            StatusId = entry.StatusId,
            Duration = TimeSpan.FromSeconds(15),
        }).ToArray();

        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1363,
                TerritoryName = "Dancing Mad Ultimate",
                Duration = TimeSpan.FromMinutes(2),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors = tanks.Concat(healers).Concat(melee).Concat(ranged)
                .Append(new ActorRecord { Id = Boss, Name = "Kefka", Kind = ActorKind.Enemy })
                .ToArray(),
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = "test:m9-session-contracts",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static ActorRecord Player(int id, string name, string job) => new()
    {
        Id = new ActorId(id),
        Name = name,
        Kind = ActorKind.Player,
        JobAbbreviation = job,
    };

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
