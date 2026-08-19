namespace BetterDeaths.Sources.FFLogs;

using BetterDeaths.Sources;
using System;

internal static class FFLogsSourceReference
{
    private const int MaximumReportCodeLength = 256;

    public static string Create(string reportCode, int fightId)
    {
        Validate(reportCode, fightId);
        var normalizedCode = reportCode.Trim();
        return $"fflogs:report:{Uri.EscapeDataString(normalizedCode)}:fight:{fightId}";
    }

    public static void Validate(string reportCode, int fightId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportCode);
        if (reportCode.Trim().Length > MaximumReportCodeLength)
        {
            throw new ArgumentOutOfRangeException(nameof(reportCode), $"FFLogs report code must be at most {MaximumReportCodeLength} characters.");
        }

        if (fightId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fightId), "FFLogs fight ID must be positive.");
        }
    }
}

internal enum FFLogsOperation
{
    Authenticate,
    LoadReport,
    LoadFightEvents,
}

internal static class FFLogsIntegrationErrors
{
    public static PullImportError InvalidRequest()
    {
        return Error(
            PullImportErrorCategory.InvalidRequest,
            "fflogs.invalid_request",
            "The FFLogs report or fight request is invalid.");
    }

    public static PullImportError AuthenticationFailed()
    {
        return Error(
            PullImportErrorCategory.Authentication,
            "fflogs.authentication_failed",
            "FFLogs authentication failed. Check the configured authorization method and credentials.");
    }

    public static PullImportError PrivateReportUnavailable()
    {
        return Error(
            PullImportErrorCategory.Authorization,
            "fflogs.private_report_unavailable",
            "This FFLogs report is not available with the current authorization.");
    }

    public static PullImportError ReportNotFound()
    {
        return Error(
            PullImportErrorCategory.NotFound,
            "fflogs.report_not_found",
            "The requested FFLogs report or fight could not be found.");
    }

    public static PullImportError RateLimited(TimeSpan? retryAfter = null)
    {
        return new PullImportError
        {
            Category = PullImportErrorCategory.RateLimited,
            Code = "fflogs.rate_limited",
            SafeMessage = "FFLogs rate-limited the import request. Retry after the service allows more requests.",
            RetryAfter = retryAfter,
        };
    }

    public static PullImportError NetworkFailure(FFLogsOperation operation)
    {
        return Error(
            PullImportErrorCategory.Network,
            $"fflogs.network.{OperationCode(operation)}",
            $"FFLogs could not complete the {OperationDescription(operation)} because of a network failure.");
    }

    public static PullImportError ProtocolFailure(FFLogsOperation operation)
    {
        return Error(
            PullImportErrorCategory.Protocol,
            $"fflogs.protocol.{OperationCode(operation)}",
            $"FFLogs returned an unexpected response while attempting to {OperationDescription(operation)}.");
    }

    public static PullImportError Unavailable()
    {
        return Error(
            PullImportErrorCategory.Unavailable,
            "fflogs.unavailable",
            "FFLogs is currently unavailable for this import.");
    }

    private static PullImportError Error(
        PullImportErrorCategory category,
        string code,
        string message)
    {
        return new PullImportError
        {
            Category = category,
            Code = code,
            SafeMessage = message,
        };
    }

    private static string OperationCode(FFLogsOperation operation)
    {
        return operation switch
        {
            FFLogsOperation.Authenticate => "authenticate",
            FFLogsOperation.LoadReport => "load_report",
            FFLogsOperation.LoadFightEvents => "load_fight_events",
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }

    private static string OperationDescription(FFLogsOperation operation)
    {
        return operation switch
        {
            FFLogsOperation.Authenticate => "authenticate",
            FFLogsOperation.LoadReport => "load the report metadata",
            FFLogsOperation.LoadFightEvents => "load fight events",
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }
}
