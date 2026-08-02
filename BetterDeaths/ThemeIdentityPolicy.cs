using System;

namespace BetterDeaths;

internal static class ThemeIdentityPolicy
{
    public const int CurrentVersion = 1;

    public static string GetKey<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return Enum.GetName(value) ?? string.Empty;
    }

    public static bool TryResolve<TEnum>(string? key, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        foreach (var option in Enum.GetValues<TEnum>())
        {
            if (string.Equals(Enum.GetName(option), key, StringComparison.OrdinalIgnoreCase))
            {
                value = option;
                return true;
            }
        }

        return false;
    }
}
