namespace BetterDeaths.Sources.FFLogs.Client;

using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

internal readonly record struct FFLogsReportCacheKey(
    string ReportHash,
    FFLogsApiAccessKind AccessKind)
{
    public static FFLogsReportCacheKey Create(string reportCode, FFLogsApiAccessKind accessKind)
    {
        return new FFLogsReportCacheKey(FFLogsCacheIdentity.HashReportCode(reportCode), accessKind);
    }
}

internal readonly record struct FFLogsEventPageCacheKey(
    string ReportHash,
    FFLogsApiAccessKind AccessKind,
    int Revision,
    int FightId,
    double StartTimeMilliseconds,
    double EndTimeMilliseconds,
    int Limit)
{
    public static FFLogsEventPageCacheKey Create(
        string reportCode,
        FFLogsApiAccessKind accessKind,
        int revision,
        int fightId,
        double startTimeMilliseconds,
        double endTimeMilliseconds,
        int limit)
    {
        return new FFLogsEventPageCacheKey(
            FFLogsCacheIdentity.HashReportCode(reportCode),
            accessKind,
            revision,
            fightId,
            startTimeMilliseconds,
            endTimeMilliseconds,
            limit);
    }
}

internal static class FFLogsCacheIdentity
{
    public static string HashReportCode(string reportCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportCode);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(reportCode.Trim()));
        return Convert.ToHexString(bytes);
    }
}

internal interface IFFLogsImportCache
{
    bool TryGetReport(
        FFLogsReportCacheKey key,
        DateTimeOffset now,
        out FFLogsReportDocument? report);

    void SetReport(
        FFLogsReportCacheKey key,
        FFLogsReportDocument report,
        DateTimeOffset expiresAt);

    bool TryGetEventPage(
        FFLogsEventPageCacheKey key,
        out FFLogsEventPage? page);

    void SetEventPage(
        FFLogsEventPageCacheKey key,
        FFLogsEventPage page);
}

internal sealed class MemoryFFLogsImportCache : IFFLogsImportCache
{
    private readonly ConcurrentDictionary<FFLogsReportCacheKey, CachedReport> reports = new();
    private readonly ConcurrentDictionary<FFLogsEventPageCacheKey, FFLogsEventPage> pages = new();

    public bool TryGetReport(
        FFLogsReportCacheKey key,
        DateTimeOffset now,
        out FFLogsReportDocument? report)
    {
        if (reports.TryGetValue(key, out var cached) && now < cached.ExpiresAt)
        {
            report = cached.Report;
            return true;
        }

        reports.TryRemove(key, out _);
        report = null;
        return false;
    }

    public void SetReport(
        FFLogsReportCacheKey key,
        FFLogsReportDocument report,
        DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(report);
        reports[key] = new CachedReport(report, expiresAt);
    }

    public bool TryGetEventPage(
        FFLogsEventPageCacheKey key,
        out FFLogsEventPage? page)
    {
        return pages.TryGetValue(key, out page);
    }

    public void SetEventPage(
        FFLogsEventPageCacheKey key,
        FFLogsEventPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        pages[key] = page;
    }

    private sealed record CachedReport(
        FFLogsReportDocument Report,
        DateTimeOffset ExpiresAt);
}
