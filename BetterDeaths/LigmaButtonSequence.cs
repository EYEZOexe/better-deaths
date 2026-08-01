using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterDeaths;

internal sealed class LigmaButtonSequence
{
    public static readonly TimeSpan ResetAfter = TimeSpan.FromMinutes(5);

    private static readonly string[] Messages =
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
        "The button has one joke. You have unlimited optimism.",
        "Scientists remain baffled by your decision to click again.",
        "You saw Ligma and thought, Surely this time.",
        "This is less of a joke now and more of a behavioral study.",
        "The button is beginning to feel bad for you.",
        "Please understand: there is nothing behind this button but ligma balls.",
        "You are not discovering new content. You are documenting a problem.",
        "Every click brings you further from dignity and no closer to a reward.",
        "I cannot stop you. Legally, however, I must advise against another click.",
        "Twenty presses. The joke is now you.",
        "Somewhere, a lab mouse just solved this puzzle faster than you.",
        "You possess the rare ability to lose a battle against a button.",
        "The definition of insanity is clicking Ligma and expecting new dialogue.",
        "Your commitment to being disappointed is genuinely unmatched.",
        "Achievement unlocked: Ligma Balls Enthusiast.",
        "The button has filed a restraining order.",
        "This interaction is now being reviewed by the Department of Bad Decisions.",
        "You have exhausted the joke, the button, and several nearby observers.",
        "There are other parts of the app. Probably.",
        "Ligma balls. Forever. This is the life you chose.",
    ];

    private readonly List<int> elevenThroughNineteen = [];
    private readonly List<int> twentyOneThroughTwentyNine = [];
    private DateTime lastClickedAtUtc = DateTime.MinValue;
    private int clickCount;

    public LigmaButtonClick Click(DateTime nowUtc, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (ShouldReset(nowUtc))
        {
            Reset(random);
        }

        clickCount++;
        lastClickedAtUtc = nowUtc;
        var messageIndex = GetMessageIndex(clickCount);
        return new LigmaButtonClick(clickCount, Messages[messageIndex], clickCount == 1);
    }

    public static int SelectSoundEffect(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return random.Next(1, 17);
    }

    private bool ShouldReset(DateTime nowUtc)
    {
        return lastClickedAtUtc == DateTime.MinValue ||
            nowUtc < lastClickedAtUtc ||
            nowUtc - lastClickedAtUtc >= ResetAfter;
    }

    private void Reset(Random random)
    {
        clickCount = 0;
        elevenThroughNineteen.Clear();
        elevenThroughNineteen.AddRange(Enumerable.Range(10, 9));
        Shuffle(elevenThroughNineteen, random);

        twentyOneThroughTwentyNine.Clear();
        twentyOneThroughTwentyNine.AddRange(Enumerable.Range(20, 9));
        Shuffle(twentyOneThroughTwentyNine, random);
    }

    private int GetMessageIndex(int currentClickCount)
    {
        if (currentClickCount <= 10)
        {
            return currentClickCount - 1;
        }

        if (currentClickCount <= 19)
        {
            return elevenThroughNineteen[currentClickCount - 11];
        }

        if (currentClickCount == 20)
        {
            return 19;
        }

        if (currentClickCount <= 29)
        {
            return twentyOneThroughTwentyNine[currentClickCount - 21];
        }

        return 29;
    }

    private static void Shuffle(List<int> values, Random random)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}

internal readonly record struct LigmaButtonClick(
    int ClickNumber,
    string Message,
    bool ShouldPlaySound);
