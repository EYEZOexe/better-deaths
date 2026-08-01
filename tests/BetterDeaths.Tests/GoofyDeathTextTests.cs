namespace BetterDeaths.Tests;

public sealed class GoofyDeathTextTests
{
    private static readonly string[] ExpectedArsenal =
    [
        "Ligma",
        "Sugma",
        "Drillma",
        "Bofa",
        "Bophades",
        "Bophedes",
        "Sugondese",
        "Ligondese",
        "Ugondese",
        "Grabahan",
        "SawCon",
        "Sakon",
        "Sakkon",
        "Suckon",
        "Sekon",
        "Gargalon",
        "Mind Goblin",
        "Goblin",
        "Gulpin",
        "Chewons",
        "E10",
        "Eaton",
        "Bofa Dee",
        "Sergeant Botha",
        "DN",
        "SoDN",
        "UCD",
        "CDs",
        "Dee",
        "Dees",
        "Deez",
        "Grabba",
        "Bophadese",
    ];

    [Fact]
    public void ActionIdSelectsEveryCoreArsenalTerm()
    {
        for (var index = 1; index < ExpectedArsenal.Length; index++)
        {
            Assert.Equal(
                ExpectedArsenal[index],
                GoofyDeathText.GetSlangTerm((uint)index, "Ignored action name"));
        }

        Assert.Equal(
            ExpectedArsenal[0],
            GoofyDeathText.GetSlangTerm((uint)ExpectedArsenal.Length, "Ignored action name"));
    }

    [Fact]
    public void ActionNameFallbackIsStable()
    {
        var first = GoofyDeathText.GetSlangTerm(0, "Unknown action");
        var second = GoofyDeathText.GetSlangTerm(0, "Unknown action");

        Assert.Equal(first, second);
        Assert.Contains(first, ExpectedArsenal);
    }

    [Fact]
    public void AliasedNamesKeepTheActualActionVisible()
    {
        Assert.Equal("Skill Issue (Actual Fatal Action)", GoofyDeathText.FormatFatalEventName("Actual Fatal Action"));
        Assert.Equal("Sugma (Actual Lead-Up Action)", GoofyDeathText.FormatLeadUpEventName(1, "Actual Lead-Up Action"));
    }

    [Fact]
    public void PostmortemLineIsStableForTheSameDeath()
    {
        var first = GoofyDeathText.GetPostmortemLine(638_895_456_000_000_000, "Player One");
        var second = GoofyDeathText.GetPostmortemLine(638_895_456_000_000_000, "Player One");

        Assert.Equal(first, second);
        Assert.StartsWith("Postmortem: ", first);
    }
}
