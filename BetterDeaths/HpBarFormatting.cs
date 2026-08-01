namespace BetterDeaths;

using System;

internal static class HpBarFormatting
{
    public const float CompactValuesMinimumWidth = 180.0f;

    public static (uint CurrentHp, uint ShieldHp) GetEventDisplayValues(
        uint currentHp,
        uint shieldHp,
        uint? resultCurrentHp,
        uint? resultShieldHp)
    {
        return resultCurrentHp is not null && resultShieldHp is not null
            ? (resultCurrentHp.Value, resultShieldHp.Value)
            : (currentHp, shieldHp);
    }

    public static string FormatBarLabel(uint currentHp, uint shieldHp, uint maxHp, float width)
    {
        var effectiveHp = (ulong)currentHp + shieldHp;
        if (maxHp == 0)
        {
            return width >= CompactValuesMinimumWidth
                ? $"{FormatCompactAmount(currentHp)} + {FormatCompactAmount(shieldHp)}"
                : FormatCompactAmount(effectiveHp);
        }

        var percent = $"{((double)effectiveHp / maxHp) * 100.0:0}%";
        return width >= CompactValuesMinimumWidth
            ? $"{FormatCompactAmount(currentHp)} + {FormatCompactAmount(shieldHp)} ({percent})"
            : percent;
    }

    public static string FormatExact(uint currentHp, uint shieldHp, uint maxHp)
    {
        var effectiveHp = (ulong)currentHp + shieldHp;
        return maxHp == 0
            ? $"{currentHp:N0} + {shieldHp:N0} shield"
            : $"{currentHp:N0} + {shieldHp:N0} shield / {maxHp:N0} ({((double)effectiveHp / maxHp) * 100.0:0}%)";
    }

    public static string FormatExactValues(uint currentHp, uint shieldHp, uint maxHp)
    {
        return maxHp == 0
            ? $"{currentHp:N0} + {shieldHp:N0} shield"
            : $"{currentHp:N0} + {shieldHp:N0} shield / {maxHp:N0}";
    }

    private static string FormatCompactAmount(ulong amount)
    {
        if (amount >= 1_000_000)
        {
            return $"{amount / 1_000_000.0:0.#}m";
        }

        return amount >= 1_000
            ? $"{amount / 1_000.0:0.#}k"
            : amount.ToString("N0");
    }
}
