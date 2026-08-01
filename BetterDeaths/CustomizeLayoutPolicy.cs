namespace BetterDeaths;

using System;

internal static class CustomizeLayoutPolicy
{
    public const float TwoColumnMinimumWidth = 520.0f;
    public const float ThemeTileWidth = 96.0f;

    public static bool UseTwoColumns(float availableWidth)
    {
        return availableWidth >= TwoColumnMinimumWidth;
    }

    public static int GetThemeTilesPerRow(float availableWidth, float itemSpacing, int themeCount)
    {
        if (themeCount <= 0)
        {
            return 0;
        }

        var safeWidth = MathF.Max(1.0f, availableWidth);
        var safeSpacing = MathF.Max(0.0f, itemSpacing);
        var count = (int)MathF.Floor((safeWidth + safeSpacing) / (ThemeTileWidth + safeSpacing));
        return Math.Clamp(count, 1, themeCount);
    }

    public static float GetThemeTileWidth(float availableWidth)
    {
        return MathF.Min(ThemeTileWidth, MathF.Max(1.0f, availableWidth));
    }
}
