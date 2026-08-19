namespace BetterDeaths;

using BetterDeaths.Analysis.Jobs;
using BetterDeaths.Analysis.Jobs.Dancer;

public sealed class JobDefinitionTests
{
    [Fact]
    public void DancerDefinitionExposesStableIdentityAndCoreData()
    {
        var definition = DancerJobDefinition.Definition;

        Assert.Equal("dnc", definition.Key);
        Assert.Equal("Dancer", definition.DisplayName);
        Assert.Equal("DNC", definition.JobAbbreviation);

        Assert.Equal((uint)15997, definition.Action(DancerJobDefinition.StandardStep).ActionId);
        Assert.Equal(TimeSpan.FromSeconds(30), definition.Action(DancerJobDefinition.StandardStep).Cooldown);
        Assert.True(definition.Action(DancerJobDefinition.StandardStep).IsGcd);

        Assert.Equal((uint)15998, definition.Action(DancerJobDefinition.TechnicalStep).ActionId);
        Assert.Equal(TimeSpan.FromSeconds(120), definition.Action(DancerJobDefinition.TechnicalStep).Cooldown);
        Assert.Equal((uint)16011, definition.Action(DancerJobDefinition.Devilment).ActionId);
        Assert.False(definition.Action(DancerJobDefinition.Devilment).IsGcd);
        Assert.Equal((uint)16013, definition.Action(DancerJobDefinition.Flourish).ActionId);
        Assert.Equal(TimeSpan.FromSeconds(60), definition.Action(DancerJobDefinition.Flourish).Cooldown);

        Assert.Equal((uint)1824, definition.Status(DancerJobDefinition.DancePartnerStatus).StatusId);
        Assert.Null(definition.Status(DancerJobDefinition.DancePartnerStatus).Duration);
        Assert.Equal((uint)1822, definition.Status(DancerJobDefinition.TechnicalFinishStatus).StatusId);
        Assert.Equal(TimeSpan.FromSeconds(20), definition.Status(DancerJobDefinition.TechnicalFinishStatus).Duration);
        Assert.Equal((uint)3869, definition.Status(DancerJobDefinition.DanceOfTheDawnReady).StatusId);
    }

    [Fact]
    public void DefinitionNormalizesJobAbbreviationAndRejectsUnknownKeys()
    {
        var definition = new JobDefinition(
            "example",
            "Example",
            " dnc ",
            [new JobActionDefinition { Key = "action", ActionId = 1, IsGcd = true }],
            [new JobStatusDefinition { Key = "status", StatusId = 2 }]);

        Assert.Equal("DNC", definition.JobAbbreviation);
        Assert.Throws<KeyNotFoundException>(() => definition.Action("missing"));
        Assert.Throws<KeyNotFoundException>(() => definition.Status("missing"));
    }

    [Fact]
    public void DefinitionRejectsDuplicateActionKeyOrId()
    {
        Assert.Throws<ArgumentException>(() => new JobDefinition(
            "example",
            "Example",
            "EX",
            [
                new JobActionDefinition { Key = "same", ActionId = 1, IsGcd = true },
                new JobActionDefinition { Key = "same", ActionId = 2, IsGcd = false },
            ],
            []));

        Assert.Throws<ArgumentException>(() => new JobDefinition(
            "example",
            "Example",
            "EX",
            [
                new JobActionDefinition { Key = "one", ActionId = 1, IsGcd = true },
                new JobActionDefinition { Key = "two", ActionId = 1, IsGcd = false },
            ],
            []));
    }

    [Fact]
    public void DefinitionRejectsDuplicateStatusKeyOrId()
    {
        Assert.Throws<ArgumentException>(() => new JobDefinition(
            "example",
            "Example",
            "EX",
            [],
            [
                new JobStatusDefinition { Key = "same", StatusId = 1 },
                new JobStatusDefinition { Key = "same", StatusId = 2 },
            ]));

        Assert.Throws<ArgumentException>(() => new JobDefinition(
            "example",
            "Example",
            "EX",
            [],
            [
                new JobStatusDefinition { Key = "one", StatusId = 1 },
                new JobStatusDefinition { Key = "two", StatusId = 1 },
            ]));
    }

    [Fact]
    public void DefinitionRejectsInvalidCooldownDurationAndCharges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JobDefinition(
            "example",
            "Example",
            "EX",
            [new JobActionDefinition
            {
                Key = "action",
                ActionId = 1,
                IsGcd = true,
                Cooldown = TimeSpan.Zero,
            }],
            []));

        Assert.Throws<ArgumentOutOfRangeException>(() => new JobDefinition(
            "example",
            "Example",
            "EX",
            [new JobActionDefinition
            {
                Key = "action",
                ActionId = 1,
                IsGcd = true,
                Charges = 0,
            }],
            []));

        Assert.Throws<ArgumentOutOfRangeException>(() => new JobDefinition(
            "example",
            "Example",
            "EX",
            [],
            [new JobStatusDefinition
            {
                Key = "status",
                StatusId = 1,
                Duration = TimeSpan.Zero,
            }]));
    }

    [Fact]
    public void JobDefinitionLayerHasNoSourceUiOrPersistenceDependencies()
    {
        var files = new[]
        {
            "BetterDeaths/Analysis/Jobs/JobDefinition.cs",
            "BetterDeaths/Analysis/Jobs/Dancer/DancerJobDefinition.cs",
        };

        foreach (var relativePath in files)
        {
            var source = ReadRepositoryFile(relativePath);
            Assert.DoesNotContain("FFLogs", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Dalamud", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ImGui", source, StringComparison.Ordinal);
            Assert.DoesNotContain("IPullStore", source, StringComparison.Ordinal);
            Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        }
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var testSourcePath = typeof(JobDefinitionTests).Assembly.Location;
        var testOutput = Path.GetDirectoryName(testSourcePath)
            ?? throw new InvalidOperationException("Could not resolve test output directory.");

        var current = new DirectoryInfo(testOutput);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "BetterDeaths.sln")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException("Could not locate repository root.");
        }

        return File.ReadAllText(Path.Combine(
            current.FullName,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
