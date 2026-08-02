namespace BetterDeaths;

public sealed class LeadUpDisplayPolicyTests
{
    [Theory]
    [InlineData(DeathEventKind.Heal)]
    [InlineData(DeathEventKind.Damage)]
    [InlineData(DeathEventKind.Status)]
    [InlineData(null)]
    public void ShowingHealingEventsKeepsEveryLeadUpRow(DeathEventKind? kind)
    {
        Assert.True(LeadUpDisplayPolicy.ShouldShowEvent(kind, true));
    }

    [Theory]
    [InlineData(DeathEventKind.Damage)]
    [InlineData(DeathEventKind.Status)]
    [InlineData(null)]
    public void HidingHealingEventsKeepsNonHealingRows(DeathEventKind? kind)
    {
        Assert.True(LeadUpDisplayPolicy.ShouldShowEvent(kind, false));
    }

    [Fact]
    public void HidingHealingEventsRemovesHealingRows()
    {
        Assert.False(LeadUpDisplayPolicy.ShouldShowEvent(DeathEventKind.Heal, false));
    }
}
