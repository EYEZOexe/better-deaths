namespace BetterDeaths;

internal static class ReplayStatusPanelPolicy
{
    private const int AlliancePartyIndexBase = 1000;

    public static bool IsLocalPartyIndex(int partyIndex)
    {
        return partyIndex >= 0 && partyIndex < AlliancePartyIndexBase;
    }

    public static float GetStackedHeight(
        bool hasPartyHp,
        float partyHpHeight,
        bool hasMitigation,
        float mitigationHeight,
        float panelGap)
    {
        return (hasPartyHp ? partyHpHeight : 0.0f) +
            (hasMitigation ? mitigationHeight : 0.0f) +
            (hasPartyHp && hasMitigation ? panelGap : 0.0f);
    }
}
