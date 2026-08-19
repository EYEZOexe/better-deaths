namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Jobs.Dancer;
using BetterDeaths.Analysis.Sessions;
using BetterDeaths.Domain;
using System.Runtime.CompilerServices;

public sealed class DancerRuleKeyTests
{
    [Fact]
    public void EveryDancerResultInitializerDefinesAnExplicitRuleKey()
    {
        foreach (var path in new[]
                 {
                     "BetterDeaths/Analysis/Jobs/Dancer/DancerCoreExecutionAnalyzer.cs",
                     "BetterDeaths/Analysis/Jobs/Dancer/DancerBurstAndUptimeAnalyzer.cs",
                 })
        {
            var source = ReadRepositoryFile(path);
            var initializers = source.Split("new AnalysisResult", StringSplitOptions.None).Skip(1).ToArray();
            Assert.NotEmpty(initializers);
            Assert.All(initializers, initializer =>
            {
                var resultBody = initializer.Split("});", 2, StringSplitOptions.None)[0];
                Assert.Contains("RuleKey =", resultBody, StringComparison.Ordinal);
            });
        }
    }

    [Fact]
    public void RuleFamiliesUseStableSemanticConstantsOrDefinitionKeys()
    {
        var fixedKeys = new[]
        {
            DancerCoreExecutionAnalyzer.StandardDanceUnderstepRuleKey,
            DancerCoreExecutionAnalyzer.TechnicalDanceUnderstepRuleKey,
            DancerCoreExecutionAnalyzer.PartnerObservedRuleKey,
            DancerCoreExecutionAnalyzer.PartnerConflictRuleKey,
            DancerBurstAndUptimeAnalyzer.DevilmentOutsideTechnicalRuleKey,
            DancerBurstAndUptimeAnalyzer.DevilmentDelayedTechnicalRuleKey,
            DancerBurstAndUptimeAnalyzer.TargetableGcdGapRuleKey,
        };

        Assert.Equal(fixedKeys.Length, fixedKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(fixedKeys, key =>
        {
            Assert.False(string.IsNullOrWhiteSpace(key));
            Assert.DoesNotContain("actor", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pull", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("time", key, StringComparison.OrdinalIgnoreCase);
        });

        Assert.Equal("proc.expired-unused", DancerCoreExecutionAnalyzer.UnusedProcRuleKeyPrefix);
        Assert.Equal("cooldown.additional-opportunity", DancerBurstAndUptimeAnalyzer.CooldownAdditionalOpportunityRulePrefix);
        Assert.Equal("cooldown.active-drift", DancerBurstAndUptimeAnalyzer.CooldownActiveDriftRulePrefix);
    }

    [Fact]
    public async Task UndersteppedDanceUsesSameRecurrenceKeyAcrossLocalAndFFLogsCanonicalFacts()
    {
        var local = await AnalyzeUnderstep(PullDataSourceKind.DalamudLive);
        var imported = await AnalyzeUnderstep(PullDataSourceKind.FFLogs);

        Assert.Equal(AnalysisSeverity.Warning, local.Severity);
        Assert.Equal(DancerCoreExecutionAnalyzer.StandardDanceUnderstepRuleKey, local.RuleKey);
        Assert.Equal(local.RuleKey, imported.RuleKey);
        Assert.True(SessionFindingKey.TryCreate(local, out var localKey));
        Assert.True(SessionFindingKey.TryCreate(imported, out var importedKey));
        Assert.Equal(localKey, importedKey);

        var displayChanged = local with
        {
            Id = AnalysisResultId.New(),
            Title = "Localized display title",
            Summary = "Different presentation copy",
        };
        Assert.True(SessionFindingKey.TryCreate(displayChanged, out var displayChangedKey));
        Assert.Equal(localKey, displayChangedKey);
    }

    [Fact]
    public void CooldownRuleKeysUseImmutableDefinitionKeysRatherThanObservedValues()
    {
        var source = ReadRepositoryFile(
            "BetterDeaths/Analysis/Jobs/Dancer/DancerBurstAndUptimeAnalyzer.cs");

        Assert.Contains("$\"{CooldownAdditionalOpportunityRulePrefix}.{definition.Key}\"", source, StringComparison.Ordinal);
        Assert.Contains("$\"{CooldownActiveDriftRulePrefix}.{definition.Key}\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RuleKey = $\"{dancer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RuleKey = $\"{previousUse", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RuleKey = $\"{activeDrift", source, StringComparison.Ordinal);
    }

    private static async Task<AnalysisResult> AnalyzeUnderstep(PullDataSourceKind sourceKind)
    {
        var player = new ActorId(1);
        var boss = new ActorId(2);
        var provenance = new EventProvenance
        {
            SourceKind = sourceKind,
            SourceReference = "test:dnc-rule-key",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        };
        var pull = new RecordedPull
        {
            Id = new PullId(Guid.NewGuid()),
            Metadata = new PullMetadata
            {
                TerritoryId = 777,
                TerritoryName = "Dancer Rule Key Test",
                Duration = TimeSpan.FromSeconds(30),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord { Id = player, Name = "Dancer", Kind = ActorKind.Player, JobAbbreviation = "DNC" },
                new ActorRecord { Id = boss, Name = "Boss", Kind = ActorKind.Enemy },
            ],
            Events =
            [
                new ActionUseEvent
                {
                    Id = new EventId(1),
                    Sequence = 1,
                    PullTime = TimeSpan.FromSeconds(5),
                    SourceActorId = player,
                    TargetActorId = boss,
                    Provenance = provenance,
                    ActionId = DancerJobDefinition.Definition.Action(DancerJobDefinition.StandardStep).ActionId,
                },
                new ActionUseEvent
                {
                    Id = new EventId(2),
                    Sequence = 2,
                    PullTime = TimeSpan.FromSeconds(7),
                    SourceActorId = player,
                    TargetActorId = boss,
                    Provenance = provenance,
                    ActionId = DancerJobDefinition.Definition.Action(DancerJobDefinition.SingleStandardFinish).ActionId,
                },
            ],
            Provenance = new PullProvenance
            {
                SourceKind = sourceKind,
                SourceReference = "test:dnc-rule-key",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };

        var registry = new AnalyzerRegistry();
        registry.Register(new DancerCoreExecutionAnalyzer());
        var run = await new AnalyzerEngine(registry).AnalyzeAsync(pull);
        Assert.Empty(run.Failures);
        return Assert.Single(run.Results, result => result.Severity >= AnalysisSeverity.Warning);
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
