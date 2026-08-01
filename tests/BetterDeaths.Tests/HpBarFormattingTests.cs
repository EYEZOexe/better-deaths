namespace BetterDeaths.Tests;

using System.Globalization;

public sealed class HpBarFormattingTests
{
    [Fact]
    public void WideBarsUseOneCompactValueFormat()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);

        Assert.Equal(
            "226.6k + 18.2k (108%)",
            HpBarFormatting.FormatBarLabel(226_618, 18_200, 226_618, HpBarFormatting.CompactValuesMinimumWidth));
        Assert.Equal(
            "0 + 0 (0%)",
            HpBarFormatting.FormatBarLabel(0, 0, 226_618, HpBarFormatting.CompactValuesMinimumWidth));
    }

    [Fact]
    public void NarrowBarsUsePercentageForEveryValueLength()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        var width = HpBarFormatting.CompactValuesMinimumWidth - 1.0f;

        Assert.Equal("100%", HpBarFormatting.FormatBarLabel(226_618, 0, 226_618, width));
        Assert.Equal("122%", HpBarFormatting.FormatBarLabel(226_618, 49_856, 226_618, width));
        Assert.Equal("0%", HpBarFormatting.FormatBarLabel(0, 0, 226_618, width));
    }

    [Fact]
    public void ExactTooltipPreservesUnshortenedValues()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);

        Assert.Equal(
            "226,618 + 18,200 shield / 226,618 (108%)",
            HpBarFormatting.FormatExact(226_618, 18_200, 226_618));
    }

    [Fact]
    public void DamageResultReplacesPreHitValuesInTheVisibleLabel()
    {
        var display = HpBarFormatting.GetEventDisplayValues(226_618, 18_200, 0, 0);

        Assert.Equal(0u, display.CurrentHp);
        Assert.Equal(0u, display.ShieldHp);
    }

    [Fact]
    public void RowsWithoutADamageResultKeepTheirCapturedValues()
    {
        var display = HpBarFormatting.GetEventDisplayValues(117_133, 0, null, null);

        Assert.Equal(117_133u, display.CurrentHp);
        Assert.Equal(0u, display.ShieldHp);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo originalCulture = CultureInfo.CurrentCulture;

        public CultureScope(CultureInfo culture)
        {
            CultureInfo.CurrentCulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
