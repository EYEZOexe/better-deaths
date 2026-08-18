namespace BetterDeaths.Analysis.Engine;

using BetterDeaths.Domain;
using System;
using System.Security.Cryptography;
using System.Text;

internal static class StableAnalysisResultIdentity
{
    public static AnalysisResultId ForEvent(PullId pullId, string analyzerId, EventId eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analyzerId);
        var identity = $"{pullId.Value:N}|{analyzerId}|{eventId.Value}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        var guidHex = Convert.ToHexString(hash.AsSpan(0, 16));
        return new AnalysisResultId(Guid.ParseExact(guidHex, "N"));
    }
}
