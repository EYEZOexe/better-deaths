namespace BetterDeaths.Tests;

public sealed class CustomizeLayoutPolicyTests
{
    [Theory]
    [InlineData(519.0f, false)]
    [InlineData(520.0f, true)]
    [InlineData(900.0f, true)]
    public void CompactControlsUseTwoColumnsOnlyWhenThereIsRoom(float width, bool expected)
    {
        Assert.Equal(expected, CustomizeLayoutPolicy.UseTwoColumns(width));
    }

    [Theory]
    [InlineData(80.0f, 7.0f, 8, 1)]
    [InlineData(96.0f, 7.0f, 8, 1)]
    [InlineData(199.0f, 7.0f, 8, 2)]
    [InlineData(508.0f, 7.0f, 8, 5)]
    [InlineData(900.0f, 7.0f, 3, 3)]
    public void ThemeTilesWrapAtTheirFixedWidth(float width, float spacing, int themes, int expected)
    {
        Assert.Equal(expected, CustomizeLayoutPolicy.GetThemeTilesPerRow(width, spacing, themes));
    }

    [Fact]
    public void NarrowThemePickerShrinksItsSingleTileWithoutOverflowing()
    {
        Assert.Equal(64.0f, CustomizeLayoutPolicy.GetThemeTileWidth(64.0f));
        Assert.Equal(CustomizeLayoutPolicy.ThemeTileWidth, CustomizeLayoutPolicy.GetThemeTileWidth(500.0f));
    }
}
