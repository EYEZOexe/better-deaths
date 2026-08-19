namespace BetterDeaths;

using BetterDeaths.Analysis.Encounters;
using BetterDeaths.Analysis.Encounters.DancingMadUltimate;
using BetterDeaths.Domain;
using System.Runtime.CompilerServices;

public sealed class EncounterDefinitionTests
{
    [Fact]
    public void ForsakenDefinitionPinsEncounterArenaPhaseAndOpeningRules()
    {
        var encounter = ForsakenDefinition.Encounter;

        Assert.Equal("dancing-mad-ultimate", encounter.Key);
        Assert.Equal("Dancing Mad Ultimate", encounter.DisplayName);
        Assert.Equal((uint)1363, encounter.TerritoryId);
        Assert.Equal(ArenaShape.Circle, encounter.Arena.Shape);
        Assert.Equal(100.0f, encounter.Arena.CenterX);
        Assert.Equal(100.0f, encounter.Arena.CenterY);
        Assert.Equal(20.0f, encounter.Arena.RadiusOrHalfSize);
        Assert.Equal("P2 - Forsaken", encounter.Phase(ForsakenDefinition.PhaseKey).DisplayName);
        Assert.Contains("Tank↔Healer", encounter.AssignmentRule(ForsakenDefinition.RolePartnerRuleKey).Description, StringComparison.Ordinal);
        Assert.Contains("Group A", encounter.AssignmentRule(ForsakenDefinition.GroupRuleKey).Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ForsakenRelevantStatusesMapToCanonicalDebuffKinds()
    {
        Assert.Equal(ForsakenDebuffKind.Stack, ForsakenDefinition.GetDebuffKind(5084));
        Assert.Equal(ForsakenDebuffKind.Spread, ForsakenDefinition.GetDebuffKind(5085));
        Assert.Equal(ForsakenDebuffKind.Cone, ForsakenDefinition.GetDebuffKind(5086));
        Assert.Equal(ForsakenDebuffKind.Unknown, ForsakenDefinition.GetDebuffKind(9999));
        Assert.Equal(new uint[] { 5084, 5085, 5086 }, ForsakenDefinition.RelevantStatusIds.OrderBy(id => id));
    }

    [Theory]
    [InlineData(1, 3, 1)]
    [InlineData(2, 1, 1)]
    [InlineData(1, 1, 2)]
    [InlineData(3, 3, 2)]
    [InlineData(2, 2, 2)]
    [InlineData(3, 2, 3)]
    [InlineData(0, 1, 0)]
    public void ForsakenOpeningPairClassificationMatchesAuditedStrategyRule(
        int firstValue,
        int secondValue,
        int expectedValue)
    {
        var first = (ForsakenDebuffKind)firstValue;
        var second = (ForsakenDebuffKind)secondValue;
        var expected = (ForsakenPairGroup)expectedValue;
        Assert.Equal(expected, ForsakenDefinition.ClassifyOpeningPair(first, second));
    }

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(2, 1, true)]
    [InlineData(3, 4, true)]
    [InlineData(4, 3, true)]
    [InlineData(1, 3, false)]
    [InlineData(2, 4, false)]
    public void ForsakenPartnerRoleCompatibilityIsExplicit(
        int firstValue,
        int secondValue,
        bool expected)
    {
        var first = (EncounterPartyRole)firstValue;
        var second = (EncounterPartyRole)secondValue;
        Assert.Equal(expected, ForsakenDefinition.ArePartnerRolesCompatible(first, second));
    }

    [Theory]
    [InlineData("PLD", 1)]
    [InlineData("Gunbreaker", 1)]
    [InlineData("WHM", 2)]
    [InlineData("Sage", 2)]
    [InlineData("DRG", 3)]
    [InlineData("Viper", 3)]
    [InlineData("DNC", 4)]
    [InlineData("Black Mage", 4)]
    [InlineData("PCT", 4)]
    [InlineData("BLU", 0)]
    [InlineData(null, 0)]
    public void PartyRoleResolverMapsCanonicalJobAbbreviationsDeterministically(
        string? job,
        int expectedValue)
    {
        Assert.Equal((EncounterPartyRole)expectedValue, EncounterPartyRoleResolver.Resolve(job));
    }

    [Fact]
    public void NonPlayerActorDoesNotGainPartyRoleFromJobText()
    {
        var actor = new ActorRecord
        {
            Id = new ActorId(1),
            Name = "Boss",
            Kind = ActorKind.Enemy,
            JobAbbreviation = "PLD",
        };

        Assert.Equal(EncounterPartyRole.Unknown, EncounterPartyRoleResolver.Resolve(actor));
    }

    [Fact]
    public void EncounterDefinitionRejectsInvalidGeometryAndDuplicateKeys()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EncounterDefinition(
            "test",
            "Test",
            1,
            new ArenaGeometry
            {
                Shape = ArenaShape.Circle,
                CenterX = 0,
                CenterY = 0,
                RadiusOrHalfSize = 0,
            },
            [],
            []));

        Assert.Throws<ArgumentException>(() => new EncounterDefinition(
            "test",
            "Test",
            1,
            ValidArena(),
            [
                new EncounterPhaseDefinition { Key = "same", DisplayName = "One" },
                new EncounterPhaseDefinition { Key = "same", DisplayName = "Two" },
            ],
            []));

        Assert.Throws<ArgumentException>(() => new EncounterDefinition(
            "test",
            "Test",
            1,
            ValidArena(),
            [],
            [
                new AssignmentRule { Key = "same", Description = "One" },
                new AssignmentRule { Key = "same", Description = "Two" },
            ]));
    }

    [Fact]
    public void EncounterDefinitionLayerHasNoSourceUiPersistenceOrLegacyReplayDependency()
    {
        var files = new[]
        {
            "BetterDeaths/Analysis/Encounters/EncounterDefinition.cs",
            "BetterDeaths/Analysis/Encounters/EncounterPartyRole.cs",
            "BetterDeaths/Analysis/Encounters/DancingMadUltimate/ForsakenDefinition.cs",
        };

        foreach (var relativePath in files)
        {
            var source = ReadRepositoryFile(relativePath);
            Assert.DoesNotContain("FFLogs", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Dalamud", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ImGui", source, StringComparison.Ordinal);
            Assert.DoesNotContain("IPullStore", source, StringComparison.Ordinal);
            Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ReplayEncounterModules", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ReplayMarkerSnapshot", source, StringComparison.Ordinal);
        }
    }

    private static ArenaGeometry ValidArena()
    {
        return new ArenaGeometry
        {
            Shape = ArenaShape.Circle,
            CenterX = 100,
            CenterY = 100,
            RadiusOrHalfSize = 20,
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
}
