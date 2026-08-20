namespace BetterDeaths.Sources.FFLogs;

using System;

internal static class FFLogsCredentialInput
{
    public static string NormalizeClientId(string clientId)
    {
        return Normalize(clientId, nameof(clientId));
    }

    public static string NormalizeClientSecret(string clientSecret)
    {
        return Normalize(clientSecret, nameof(clientSecret));
    }

    private static string Normalize(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}
