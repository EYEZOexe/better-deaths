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
        return Create($"{pullId.Value:N}|{analyzerId}|{eventId.Value}");
    }

    public static AnalysisResultId ForActorWindow(
        PullId pullId,
        string analyzerId,
        ActorId actorId,
        TimeRange timeRange,
        string discriminator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analyzerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(discriminator);
        if (timeRange.End < timeRange.Start)
        {
            throw new ArgumentException("Analysis time range end cannot precede start.", nameof(timeRange));
        }

        return Create(
            $"{pullId.Value:N}|{analyzerId}|actor-window|{actorId.Value}|" +
            $"{timeRange.Start.Ticks}|{timeRange.End.Ticks}|{discriminator}");
    }

    private static AnalysisResultId Create(string identity)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        var guidHex = Convert.ToHexString(hash.AsSpan(0, 16));
        return new AnalysisResultId(Guid.ParseExact(guidHex, "N"));
    }
}
