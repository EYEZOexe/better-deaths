namespace BetterDeaths;

public sealed class DmuReplayDataTests
{
    [Theory]
    [InlineData(0x14, 100.0f, 100.0f)]
    [InlineData(0x15, 113.5f, 100.0f)]
    [InlineData(0x16, 100.0f, 86.5f)]
    [InlineData(0x17, 86.5f, 100.0f)]
    [InlineData(0x18, 100.0f, 113.5f)]
    [InlineData(0x19, 113.5f, 113.5f)]
    [InlineData(0x1A, 113.5f, 86.5f)]
    [InlineData(0x1B, 86.5f, 86.5f)]
    [InlineData(0x1C, 86.5f, 113.5f)]
    public void P5ArenaHoleMapEffectIndexesResolveToExpectedPositions(int index, float expectedX, float expectedZ)
    {
        Assert.True(ReplayEncounterModules.TryGetDmuP5ArenaHolePosition((uint)index, out var x, out var z));
        Assert.Equal(expectedX, x);
        Assert.Equal(expectedZ, z);
    }

    [Theory]
    [InlineData(0x13)]
    [InlineData(0x1D)]
    public void P5ArenaHoleMapEffectRejectsUnknownIndexes(int index)
    {
        Assert.False(ReplayEncounterModules.TryGetDmuP5ArenaHolePosition((uint)index, out _, out _));
    }

    [Fact]
    public void P5ArenaHoleUsesBossModStateAndRadius()
    {
        Assert.Equal(0x00200010u, ReplayEncounterModules.DmuP5ArenaHoleMapEffectState);
        Assert.Equal(8.0f, ReplayEncounterModules.DmuP5ArenaHoleRadius);
    }

    [Theory]
    [InlineData(717)]
    [InlineData(5086)]
    public void P2ForsakenConeUsesFortyYalmNinetyDegreeGeometry(uint markerId)
    {
        var module = ReplayEncounterModules.Get(1363);

        Assert.True(module.TryGetMarkerInfo(markerId, out var info));
        Assert.Equal(ReplayMechanicShape.Cone, info.Shape);
        Assert.Equal(40.0f, info.Radius);
        Assert.Equal(40.0f, info.Length);
        Assert.Equal(90.0f, info.AngleDegrees);
    }

    [Fact]
    public void P2ForsakenStatusUsesSpreadShape()
    {
        var module = ReplayEncounterModules.Get(1363);

        Assert.True(module.TryGetMarkerInfo(5085, out var info));
        Assert.Equal(ReplayMechanicShape.Spread, info.Shape);
        Assert.Equal(5.0f, info.Radius);
    }

    [Fact]
    public void P5ForsakenBondsMarkerUsesSixYalmFivePointOneSecondStack()
    {
        var module = ReplayEncounterModules.Get(1363);

        Assert.True(module.TryGetMarkerInfo(161, out var info));
        Assert.Equal(ReplayMechanicShape.Stack, info.Shape);
        Assert.Equal(6.0f, info.Radius);
        Assert.Equal(5.1f, info.DurationSeconds);
    }
}
