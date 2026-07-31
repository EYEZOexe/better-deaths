namespace BetterDeaths;

public sealed class ReplayMechanicCatalogTests
{
    [Fact]
    public void GeneratedCatalogContainsEveryAuditedUltimate()
    {
        Assert.Equal(1176, BossModUltimateCatalog.ActionCount);
        Assert.Equal(1722, BossModUltimateCatalog.IdentifierCount);
        Assert.Equal(
            ["DSR", "FRU", "TEA", "TOP", "UCOB", "UMAD", "UWU"],
            BossModUltimateCatalog.EncounterNames);
        Assert.Equal("d3f731831f214ffa3f9eee697361dd39c4e704a0", BossModUltimateCatalog.SourceCommit);
    }

    [Fact]
    public void BossModGeometryIsScopedByTerritoryAndAction()
    {
        Assert.True(BossModUltimateCatalog.TryGetAction(733, 9913, out var action));

        Assert.Equal("UCOB", action.EncounterName);
        Assert.Equal("ThermionicBurst", action.Name);
        Assert.Equal(ReplayMechanicShape.Cone, action.Geometry?.Shape);
        Assert.Equal(24.0f, action.Geometry?.Length);
        Assert.Equal(22.5f, action.Geometry?.AngleDegrees);
        Assert.False(BossModUltimateCatalog.TryGetAction(777, 9913, out _));
    }

    [Fact]
    public void ReusedTetherIdKeepsEncounterSpecificMeanings()
    {
        var dsr = BossModUltimateCatalog.FindIdentifiers(968, ReplayCatalogIdentifierKind.Tether, 84);
        var top = BossModUltimateCatalog.FindIdentifiers(1122, ReplayCatalogIdentifierKind.Tether, 84);
        var fru = BossModUltimateCatalog.FindIdentifiers(1238, ReplayCatalogIdentifierKind.Tether, 84);
        var umad = BossModUltimateCatalog.FindIdentifiers(1363, ReplayCatalogIdentifierKind.Tether, 84);

        Assert.Equal(["HolyBladedance", "HolyShieldBash"], dsr.Select(identifier => identifier.Name));
        Assert.Equal("OptimizedBladedance", Assert.Single(top).Name);
        Assert.Equal("HiemalRay", Assert.Single(fru).Name);
        Assert.Equal("BlackHole", Assert.Single(umad).Name);
    }

    [Fact]
    public void ExplicitFakeActionIsCatalogedWithoutGeometry()
    {
        Assert.True(BossModUltimateCatalog.TryGetAction(1363, 47771, out var action));
        Assert.Equal("BlizzardIIIBlowoutFake", action.Name);
        Assert.Null(action.Geometry);
    }

    [Theory]
    [InlineData(2, 8, 2, 0, false, ReplayMechanicShape.Circle, 10.0f, 0)]
    [InlineData(2, 8, 0, 25, false, ReplayMechanicShape.Circle, 8.0f, 1)]
    [InlineData(7, 6, 0, 25, true, ReplayMechanicShape.Circle, 6.0f, 1)]
    [InlineData(12, 40, 10, 0, false, ReplayMechanicShape.Line, 40.0f, 0)]
    [InlineData(13, 40, 0, 0, false, ReplayMechanicShape.Cone, 40.0f, 0)]
    public void ActionSheetInferenceSupportsCommonShapes(
        byte castType,
        byte effectRange,
        byte xAxisModifier,
        sbyte range,
        bool targetArea,
        ReplayMechanicShape expectedShape,
        float expectedExtent,
        int expectedAnchor)
    {
        Assert.True(ReplayMechanicCatalog.TryInferActionSheetGeometry(
            castType,
            effectRange,
            xAxisModifier,
            range,
            targetArea,
            out var geometry,
            out var anchor));

        Assert.Equal(expectedShape, geometry.Shape);
        Assert.Equal(expectedExtent, geometry.Shape == ReplayMechanicShape.Line ? geometry.Length : geometry.Radius);
        Assert.Equal((ReplayMechanicAnchor)expectedAnchor, anchor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(11)]
    public void ActionSheetInferenceRejectsAmbiguousShapes(byte castType)
    {
        Assert.False(ReplayMechanicCatalog.TryInferActionSheetGeometry(
            castType,
            10,
            2,
            0,
            false,
            out _,
            out _));
    }

    [Fact]
    public void CatalogGeometryOverridesMissingSheetGeometry()
    {
        Assert.True(ReplayMechanicCatalog.TryResolve(
            1238,
            40202,
            "_rsv_40202",
            0,
            0,
            0,
            0,
            false,
            out var mechanic));

        Assert.Equal("Axe Kick", mechanic.Label);
        Assert.Equal(ReplayMechanicShape.Circle, mechanic.Geometry.Shape);
        Assert.Equal(16.0f, mechanic.Geometry.Radius);
        Assert.True(mechanic.IsKnown);
        Assert.StartsWith("BossMod@", mechanic.Provenance);
    }

    [Theory]
    [InlineData(733, "UCOB", 0.0f, 0.0f, 21.0f)]
    [InlineData(777, "UWU", 100.0f, 100.0f, 20.0f)]
    [InlineData(887, "TEA", 100.0f, 100.0f, 20.0f)]
    [InlineData(968, "DSR", 100.0f, 100.0f, 21.0f)]
    [InlineData(1122, "TOP", 100.0f, 100.0f, 20.0f)]
    [InlineData(1238, "FRU", 100.0f, 100.0f, 20.0f)]
    public void UltimateModulesProvideBaseArena(
        uint territoryId,
        string expectedName,
        float expectedX,
        float expectedZ,
        float expectedRadius)
    {
        var module = ReplayEncounterModules.Get(territoryId);

        Assert.Equal(expectedName, module.Name);
        Assert.True(module.TryGetReplayArena([], [], DateTime.UtcNow, out var arena));
        Assert.Equal(expectedX, arena.CenterX);
        Assert.Equal(expectedZ, arena.CenterZ);
        Assert.Equal(expectedRadius, arena.Radius);
    }

    [Theory]
    [InlineData(733, 39, "Megaflare Stack", ReplayMechanicShape.Stack)]
    [InlineData(887, 139, "Optical Sight Spread", ReplayMechanicShape.Spread)]
    [InlineData(887, 62, "Optical Sight Stack", ReplayMechanicShape.Stack)]
    public void ExplicitUltimateStackAndSpreadIconsCreateGenericMarkerGeometry(
        uint territoryId,
        uint markerId,
        string expectedLabel,
        ReplayMechanicShape expectedShape)
    {
        var module = ReplayEncounterModules.Get(territoryId);

        Assert.True(module.TryGetMarkerInfo(markerId, out var info));
        Assert.Equal(expectedLabel, info.ShortLabel);
        Assert.Equal(expectedShape, info.Shape);
        Assert.Equal(5.0f, info.Radius);
    }

    [Fact]
    public void ReusedIconDoesNotBorrowMeaningFromAnotherUltimate()
    {
        var tea = ReplayEncounterModules.Get(887);
        var fru = ReplayEncounterModules.Get(1238);

        Assert.True(tea.TryGetMarkerInfo(62, out var teaInfo));
        Assert.Equal(ReplayMechanicShape.Stack, teaInfo.Shape);
        Assert.True(fru.TryGetMarkerInfo(62, out var fruInfo));
        Assert.Equal("Delayed Dark Water", fruInfo.ShortLabel);
        Assert.Null(fruInfo.Shape);
    }

    [Fact]
    public void DmuUsesCatalogForMarkersOutsideItsCustomRules()
    {
        var dmu = ReplayEncounterModules.Get(1363);

        Assert.True(dmu.TryGetMarkerInfo(218, out var importedInfo));
        Assert.Equal("Tankbuster", importedInfo.ShortLabel);
        Assert.Equal("Dancing Mad Ultimate: Tankbuster", importedInfo.Description);
        Assert.Null(importedInfo.Shape);

        Assert.True(dmu.TryGetMarkerInfo(127, out var customInfo));
        Assert.Equal("Spread", customInfo.ShortLabel);
        Assert.Equal("Fire spread", customInfo.Description);
        Assert.Equal(ReplayMechanicShape.Spread, customInfo.Shape);
    }
}
