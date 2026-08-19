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
    [InlineData(ForsakenDebuffKind.Stack, ForsakenDebuffKind.Cone, ForsakenPairGroup.GroupA)]
    [InlineData(ForsakenDebuffKind.Spread, ForsakenDebuffKind.Stack, ForsakenPairGroup.GroupA)]
    [InlineData(ForsakenDebuffKind.Stack, ForsakenDebuffKind.Stack, ForsakenPairGroup.GroupB)]
    [InlineData(ForsakenDebuffKind.Cone, ForsakenDebuffKind.Cone, ForsakenPairGroup.GroupB)]
    [InlineData(ForsakenDebuffKind.Spread, ForsakenDebuffKind.Spread, ForsakenPairGroup.GroupB)]
    [InlineData(ForsakenDebuffKind.Cone, ForsakenDebuffKind.Spread, ForsakenPairGroup.Incompatible)]
    [InlineData(ForsakenDebuffKind.Unknown, ForsakenDebuffKind.Stack, ForsakenPairGroup.Unknown)]
    public void ForsakenOpeningPairClassificationMatchesAuditedStrategyRule(
        ForsakenDebuffKind first,
        ForsakenDebuffKind second,
        ForsakenPairGroup expected)
    {
        Assert.Equal(expected, ForsakenDefinition.ClassifyOpeningPair(first, second));
    }

    [Theory]
    [InlineData(EncounterPartyRole.Tank, EncounterPartyRole.Healer, true)]
    [InlineData(EncounterPartyRole.Healer, EncounterPartyRole.Tank, true)]
    [InlineData(EncounterPartyRole.Melee, EncounterPartyRole.Ranged, true)]
    [InlineData(EncounterPartyRole.Ranged, EncounterPartyRole.Melee, true)]
    [InlineData(EncounterPartyRole.Tank, EncounterPartyRole.Melee, false)]
    [InlineData(EncounterPartyRole.Healer, EncounterPartyRole.Ranged, false)]
    public void ForsakenPartnerRoleCompatibilityIsExplicit(
        EncounterPartyRole first,
        EncounterPartyRole second,
        bool expected)
    {
        Assert.Equal(expected, ForsakenDefinition.ArePartnerRolesCompatible(first, second));
    }

    [Theory]
    [InlineData("PLD", EncounterPartyRole.Tank)]
    [InlineData("Gunbreaker", EncounterPartyRole.Tank)]
    [InlineData("WHM", EncounterPartyRole.Healer)]
    [InlineData("Sage", EncounterPartyRole.Healer)]
    [InlineData("DRG", EncounterPartyRole.Melee)]
    [InlineData("Viper", EncounterPartyRole.Melee)]
    [InlineData("DNC", EncounterPartyRole.Ranged)]
    [InlineData("Black Mage", EncounterPartyRole.Ranged)]
    [InlineData("PCT", EncounterPartyRole.Ranged)]
    [InlineData("BLU", EncounterPartyRole.Unknown)]
    [InlineData(null, EncounterPartyRole.Unknown)]
    public void PartyRoleResolverMapsCanonicalJobAbbreviationsDeterministically(
        string? job,
        EncounterPartyRole expected)
    {
        Assert.Equal(expected, EncounterPartyRoleResolver.Resolve(job));
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
