namespace BetterDeaths.Tests;

public sealed class LigmaButtonSequenceTests
{
    private static readonly DateTime StartedAtUtc = new(2026, 7, 31, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FixedMilestonesAndRandomizedBucketsUseTheExpectedMessages()
    {
        var sequence = new LigmaButtonSequence();
        var random = new Random(886);
        var clicks = Enumerable.Range(0, 31)
            .Select(index => sequence.Click(StartedAtUtc.AddSeconds(index), random))
            .ToList();

        Assert.Equal(ExpectedFirstTen, clicks.Take(10).Select(click => click.Message));
        Assert.Equal("Twenty presses. The joke is now you.", clicks[19].Message);
        Assert.Equal("Ligma balls. Forever. This is the life you chose.", clicks[29].Message);
        Assert.Equal(clicks[29].Message, clicks[30].Message);

        Assert.Equal(
            ExpectedElevenThroughNineteen.Order(),
            clicks.Skip(10).Take(9).Select(click => click.Message).Order());
        Assert.Equal(
            ExpectedTwentyOneThroughTwentyNine.Order(),
            clicks.Skip(20).Take(9).Select(click => click.Message).Order());
    }

    [Fact]
    public void SoundOnlyPlaysOnFirstClickUntilFiveMinutesOfInactivity()
    {
        var sequence = new LigmaButtonSequence();
        var random = new Random(896);

        var first = sequence.Click(StartedAtUtc, random);
        var second = sequence.Click(StartedAtUtc.AddMinutes(4), random);
        var reset = sequence.Click(StartedAtUtc.AddMinutes(9), random);

        Assert.True(first.ShouldPlaySound);
        Assert.False(second.ShouldPlaySound);
        Assert.True(reset.ShouldPlaySound);
        Assert.Equal(1, reset.ClickNumber);
        Assert.Equal(first.Message, reset.Message);
    }

    [Fact]
    public void SelectedSoundEffectIsValidForEchoChat()
    {
        var random = new Random(42);

        for (var index = 0; index < 100; index++)
        {
            Assert.InRange(LigmaButtonSequence.SelectSoundEffect(random), 1, 16);
        }
    }

    private static readonly string[] ExpectedElevenThroughNineteen =
    [
        "The button has one joke. You have unlimited optimism.",
        "Scientists remain baffled by your decision to click again.",
        "You saw Ligma and thought, Surely this time.",
        "This is less of a joke now and more of a behavioral study.",
        "The button is beginning to feel bad for you.",
        "Please understand: there is nothing behind this button but ligma balls.",
        "You are not discovering new content. You are documenting a problem.",
        "Every click brings you further from dignity and no closer to a reward.",
        "I cannot stop you. Legally, however, I must advise against another click.",
    ];

    private static readonly string[] ExpectedFirstTen =
    [
        "Ligma balls. What were you expecting?",
        "You clicked it again. Ligma balls. Still.",
        "There was no secret second joke. It was ligma balls again.",
        "Remarkable. The button labeled Ligma produced ligma balls.",
        "Your curiosity is inspiring. Your pattern recognition is not.",
        "You have now voluntarily fallen for the same joke five times.",
        "Maybe the next click will be different. It will not, but maybe.",
        "At this point, you are participating in your own downfall.",
        "I admire your persistence, if not your judgment.",
        "Ten clicks. No lessons learned. Incredible.",
    ];

    private static readonly string[] ExpectedTwentyOneThroughTwentyNine =
    [
        "Somewhere, a lab mouse just solved this puzzle faster than you.",
        "You possess the rare ability to lose a battle against a button.",
        "The definition of insanity is clicking Ligma and expecting new dialogue.",
        "Your commitment to being disappointed is genuinely unmatched.",
        "Achievement unlocked: Ligma Balls Enthusiast.",
        "The button has filed a restraining order.",
        "This interaction is now being reviewed by the Department of Bad Decisions.",
        "You have exhausted the joke, the button, and several nearby observers.",
        "There are other parts of the app. Probably.",
    ];
}
