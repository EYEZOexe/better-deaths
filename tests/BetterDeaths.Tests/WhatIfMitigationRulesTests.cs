namespace BetterDeaths.Tests;

public sealed class WhatIfMitigationRulesTests
{
    [Fact]
    public void DeduplicateStatuses_CountsDuplicateBossDebuffOnce()
    {
        var firstFeint = CreateStatus(1195, "Feint", 100);
        var secondFeint = CreateStatus(1195, "Feint", 200);

        var result = WhatIfMitigationRules.DeduplicateStatuses([firstFeint, secondFeint]);

        Assert.Equal([firstFeint], result);
    }

    [Fact]
    public void ShareStatus_MatchesSameTargetedMitigationFromDifferentSources()
    {
        var firstAquaveil = CreateStatus(2708, "Aquaveil", 100);
        var secondAquaveil = CreateStatus(2708, "Aquaveil", 200);

        Assert.True(WhatIfMitigationRules.ShareStatus([firstAquaveil], [secondAquaveil]));
    }

    [Fact]
    public void ShareStatus_DoesNotMatchDifferentMitigationEffects()
    {
        var feint = CreateStatus(1195, "Feint", 100);
        var reprisal = CreateStatus(1193, "Reprisal", 200);

        Assert.False(WhatIfMitigationRules.ShareStatus([feint], [reprisal]));
    }

    [Fact]
    public void DeduplicateStatuses_UsesNameForStatusesWithoutIds()
    {
        var first = CreateStatus(0, "Dark Missionary", 100);
        var second = CreateStatus(0, "dark missionary", 200);

        var result = WhatIfMitigationRules.DeduplicateStatuses([first, second]);

        Assert.Equal([first], result);
    }

    private static StatusSnapshot CreateStatus(uint id, string name, uint sourceId)
    {
        return new StatusSnapshot(id, name, 0, sourceId, 0, 10.0f);
    }
}
