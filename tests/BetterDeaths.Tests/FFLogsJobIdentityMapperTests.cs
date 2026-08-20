namespace BetterDeaths;

using BetterDeaths.Domain;
using BetterDeaths.Sources.FFLogs;

public sealed class FFLogsJobIdentityMapperTests
{
    public static TheoryData<string, string> KnownJobIdentities => new()
    {
        { "Paladin", "PLD" },
        { "PLD", "PLD" },
        { "Warrior", "WAR" },
        { "WAR", "WAR" },
        { "DarkKnight", "DRK" },
        { "DRK", "DRK" },
        { "Gunbreaker", "GNB" },
        { "GNB", "GNB" },
        { "WhiteMage", "WHM" },
        { "WHM", "WHM" },
        { "Scholar", "SCH" },
        { "SCH", "SCH" },
        { "Astrologian", "AST" },
        { "AST", "AST" },
        { "Sage", "SGE" },
        { "SGE", "SGE" },
        { "Monk", "MNK" },
        { "MNK", "MNK" },
        { "Dragoon", "DRG" },
        { "DRG", "DRG" },
        { "Ninja", "NIN" },
        { "NIN", "NIN" },
        { "Samurai", "SAM" },
        { "SAM", "SAM" },
        { "Reaper", "RPR" },
        { "RPR", "RPR" },
        { "Viper", "VPR" },
        { "VPR", "VPR" },
        { "Bard", "BRD" },
        { "BRD", "BRD" },
        { "Machinist", "MCH" },
        { "MCH", "MCH" },
        { "Dancer", "DNC" },
        { "DNC", "DNC" },
        { "BlackMage", "BLM" },
        { "BLM", "BLM" },
        { "Summoner", "SMN" },
        { "SMN", "SMN" },
        { "RedMage", "RDM" },
        { "RDM", "RDM" },
        { "Pictomancer", "PCT" },
        { "PCT", "PCT" },
    };

    [Theory]
    [MemberData(nameof(KnownJobIdentities))]
    public void MapsEveryKnownFFLogsPlayerJobIdentityToCanonicalAbbreviation(
        string sourceSubType,
        string expectedAbbreviation)
    {
        var actual = FFLogsJobIdentityMapper.ToCanonicalAbbreviation(
            ActorKind.Player,
            sourceSubType);

        Assert.Equal(expectedAbbreviation, actual);
    }

    [Theory]
    [InlineData("Dark Knight", "DRK")]
    [InlineData("dark-knight", "DRK")]
    [InlineData("white-mage", "WHM")]
    [InlineData(" White Mage ", "WHM")]
    [InlineData("wHiTe MaGe", "WHM")]
    [InlineData("Black Mage", "BLM")]
    [InlineData("black-mage", "BLM")]
    [InlineData("Red Mage", "RDM")]
    [InlineData("red-mage", "RDM")]
    [InlineData("dnc", "DNC")]
    [InlineData(" PCT ", "PCT")]
    public void AcceptsKnownNameVariantsAndCanonicalAbbreviations(
        string sourceSubType,
        string expectedAbbreviation)
    {
        var actual = FFLogsJobIdentityMapper.ToCanonicalAbbreviation(
            ActorKind.Player,
            sourceSubType);

        Assert.Equal(expectedAbbreviation, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UnknownFutureJob")]
    [InlineData("LimitBreak")]
    [InlineData("DancerMain")]
    [InlineData("NotDancer")]
    [InlineData("DNC2")]
    [InlineData("WhiteMageNPC")]
    [InlineData("D-a-n-c-e-r")]
    [InlineData("Dancer!")]
    [InlineData("White_Mage")]
    public void UnknownOrMissingPlayerSubTypesRemainUnmapped(string? sourceSubType)
    {
        var actual = FFLogsJobIdentityMapper.ToCanonicalAbbreviation(
            ActorKind.Player,
            sourceSubType);

        Assert.Null(actual);
    }

    [Theory]
    [InlineData(ActorKind.Unknown)]
    [InlineData(ActorKind.Enemy)]
    [InlineData(ActorKind.Pet)]
    public void NonPlayerActorKindsNeverAcquireAJob(ActorKind actorKind)
    {
        var actual = FFLogsJobIdentityMapper.ToCanonicalAbbreviation(actorKind, "Dancer");

        Assert.Null(actual);
    }
}
