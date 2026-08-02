namespace BetterDeaths;

internal static class LeadUpDisplayPolicy
{
    public static bool ShouldShowEvent(DeathEventKind? kind, bool showHealingEvents)
    {
        return showHealingEvents || kind != DeathEventKind.Heal;
    }
}
