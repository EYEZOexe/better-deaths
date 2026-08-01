namespace BetterDeaths;

public sealed class ReplayStatusPanelPolicyTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(-1, false)]
    [InlineData(1016, false)]
    public void PartyHpOnlyIncludesTheLocalParty(int partyIndex, bool expected)
    {
        Assert.Equal(expected, ReplayStatusPanelPolicy.IsLocalPartyIndex(partyIndex));
    }

    [Fact]
    public void StackedHeightReservesMitigationSpace()
    {
        var height = ReplayStatusPanelPolicy.GetStackedHeight(
            hasPartyHp: true,
            partyHpHeight: 333.0f,
            hasMitigation: true,
            mitigationHeight: 444.0f,
            panelGap: 8.0f);

        Assert.Equal(785.0f, height);
    }
}
