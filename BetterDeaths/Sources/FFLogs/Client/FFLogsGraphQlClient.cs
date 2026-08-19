namespace BetterDeaths.Sources.FFLogs.Client;

using BetterDeaths.Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal sealed class FFLogsGraphQlClient
{
    private const string ReportMetadataQuery = """
        query AnalyzerReportMetadata($code: String!) {
          reportData {
            report(code: $code) {
              code
              startTime
              endTime
              revision
              fights {
                id
                encounterID
                name
                startTime
                endTime
                kill
                inProgress
                gameZone { id name }
              }
            }
          }
        }
        """;

    private const string FightEventsQuery = """
        query AnalyzerFightEvents($code: String!, $fightIDs: [Int], $startTime: Float, $endTime: Float, $limit: Int) {
          reportData {
            report(code: $code) {
              events(
                fightIDs: $fightIDs,
                startTime: $startTime,
                endTime: $endTime,
                limit: $limit,
                translate: false,
                useActorIDs: true,
                useAbilityIDs: true
              ) {
                data
                nextPageTimestamp
              }
            }
          }
        }
        """;

    private readonly HttpClient httpClient;
    private readonly IFFLogsAccessTokenProvider accessTokenProvider;
    private readonly FFLogsApiClientOptions options;
    private readonly IFFLogsImportCache cache;
    private readonly TimeProvider timeProvider;

    public FFLogsGraphQlClient(
        HttpClient httpClient,
        IFFLogsAccessTokenProvider accessTokenProvider,
        FFLogsApiClientOptions? options = null,
        IFFLogsImportCache? cache = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(accessTokenProvider);
        var resolvedOptions = options ?? new FFLogsApiClientOptions();
        resolvedOptions.Validate();

        this.httpClient = httpClient;
        this.accessTokenProvider = accessTokenProvider;
        this.options = resolvedOptions;
        this.cache = cache ?? new MemoryFFLogsImportCache();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<FFLogsApiResult<FFLogsReportDocument>> LoadReportAsync(
        string reportCode,
        FFLogsApiAccessKind accessKind,
        CancellationToken cancellationToken = default)
    {
        try
        {
            FFLogsSourceReference.Validate(reportCode, 1);
        }
        catch (ArgumentException)
        {
            return FFLogsApiResult<FFLogsReportDocument>.Failure(FFLogsIntegrationErrors.InvalidRequest());
        }

        var cacheKey = FFLogsReportCacheKey.Create(reportCode, accessKind);
        var now = timeProvider.GetUtcNow();
        if (cache.TryGetReport(cacheKey, now, out var cached) && cached is not null)
        {
            return FFLogsApiResult<FFLogsReportDocument>.Success(cached);
        }

        var variables = new Dictionary<string, object?>
        {
            ["code"] = reportCode.Trim(),
        };
        var response = await SendGraphQlAsync(
            accessKind,
            ReportMetadataQuery,
            variables,
            FFLogsOperation.LoadReport,
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return FFLogsApiResult<FFLogsReportDocument>.Failure(response.Error!);
        }

        try
        {
            var parsed = ParseReportDocument(response.Value);
            if (parsed is null)
            {
                return FFLogsApiResult<FFLogsReportDocument>.Failure(FFLogsIntegrationErrors.ReportNotFound());
            }

            cache.SetReport(cacheKey, parsed, now + options.ReportMetadataCacheDuration);
            return FFLogsApiResult<FFLogsReportDocument>.Success(parsed);
        }
        catch (JsonException)
        {
            return FFLogsApiResult<FFLogsReportDocument>.Failure(FFLogsIntegrationErrors.ProtocolFailure(FFLogsOperation.LoadReport));
        }
        catch (InvalidOperationException)
        {
            return FFLogsApiResult<FFLogsReportDocument>.Failure(FFLogsIntegrationErrors.ProtocolFailure(FFLogsOperation.LoadReport));
        }
    }

    public async ValueTask<FFLogsApiResult<FFLogsFightImportData>> LoadFightAsync(
        string reportCode,
        int fightId,
        FFLogsApiAccessKind accessKind,
        CancellationToken cancellationToken = default)
    {
        try
        {
            FFLogsSourceReference.Validate(reportCode, fightId);
        }
        catch (ArgumentException)
        {
            return FFLogsApiResult<FFLogsFightImportData>.Failure(FFLogsIntegrationErrors.InvalidRequest());
        }

        var reportResult = await LoadReportAsync(reportCode, accessKind, cancellationToken).ConfigureAwait(false);
        if (!reportResult.IsSuccess)
        {
            return FFLogsApiResult<FFLogsFightImportData>.Failure(reportResult.Error!);
        }

        var reportDocument = reportResult.Value!;
        var fight = reportDocument.Fights.FirstOrDefault(candidate => candidate.Id == fightId);
        if (fight is null)
        {
            return FFLogsApiResult<FFLogsFightImportData>.Failure(FFLogsIntegrationErrors.ReportNotFound());
        }

        var events = new List<FFLogsEventEnvelope>();
        var cursor = fight.StartTimeMilliseconds;
        var end = fight.EndTimeMilliseconds;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageResult = await LoadEventPageAsync(
                reportCode,
                reportDocument.Report.Revision,
                fight,
                cursor,
                end,
                accessKind,
                cancellationToken).ConfigureAwait(false);
            if (!pageResult.IsSuccess)
            {
                return FFLogsApiResult<FFLogsFightImportData>.Failure(pageResult.Error!);
            }

            var page = pageResult.Value!;
            events.AddRange(page.Events);
            if (page.NextPageTimestampMilliseconds is not { } next)
            {
                break;
            }

            if (!double.IsFinite(next) || next <= cursor || next > end)
            {
                return FFLogsApiResult<FFLogsFightImportData>.Failure(
                    FFLogsIntegrationErrors.ProtocolFailure(FFLogsOperation.LoadFightEvents));
            }

            cursor = next;
        }

        return FFLogsApiResult<FFLogsFightImportData>.Success(new FFLogsFightImportData
        {
            ReportDocument = reportDocument,
            Fight = fight,
            Events = events,
        });
    }

    private async ValueTask<FFLogsApiResult<FFLogsEventPage>> LoadEventPageAsync(
        string reportCode,
        int reportRevision,
        FFLogsFightMetadata fight,
        double startTimeMilliseconds,
        double endTimeMilliseconds,
        FFLogsApiAccessKind accessKind,
        CancellationToken cancellationToken)
    {
        var cacheKey = FFLogsEventPageCacheKey.Create(
            reportCode,
            accessKind,
            reportRevision,
            fight.Id,
            startTimeMilliseconds,
            endTimeMilliseconds,
            options.EventPageLimit);
        if (cache.TryGetEventPage(cacheKey, out var cached) && cached is not null)
        {
            return FFLogsApiResult<FFLogsEventPage>.Success(cached);
        }

        var variables = new Dictionary<string, object?>
        {
            ["code"] = reportCode.Trim(),
            ["fightIDs"] = new[] { fight.Id },
            ["startTime"] = startTimeMilliseconds,
            ["endTime"] = endTimeMilliseconds,
            ["limit"] = options.EventPageLimit,
        };
        var response = await SendGraphQlAsync(
            accessKind,
            FightEventsQuery,
            variables,
            FFLogsOperation.LoadFightEvents,
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return FFLogsApiResult<FFLogsEventPage>.Failure(response.Error!);
        }

        try
        {
            var page = ParseEventPage(response.Value);
            if (page is null)
            {
                return FFLogsApiResult<FFLogsEventPage>.Failure(FFLogsIntegrationErrors.ReportNotFound());
            }

            cache.SetEventPage(cacheKey, page);
            return FFLogsApiResult<FFLogsEventPage>.Success(page);
        }
        catch (JsonException)
        {
            return FFLogsApiResult<FFLogsEventPage>.Failure(FFLogsIntegrationErrors.ProtocolFailure(FFLogsOperation.LoadFightEvents));
        }
        catch (InvalidOperationException)
        {
            return FFLogsApiResult<FFLogsEventPage>.Failure(FFLogsIntegrationErrors.ProtocolFailure(FFLogsOperation.LoadFightEvents));
        }
    }

    private async ValueTask<FFLogsApiResult<JsonElement>> SendGraphQlAsync(
        FFLogsApiAccessKind accessKind,
        string query,
        IReadOnlyDictionary<string, object?> variables,
        FFLogsOperation operation,
        CancellationToken cancellationToken)
    {
        FFLogsAccessToken token;
        try
        {
            token = await accessTokenProvider.GetAccessTokenAsync(accessKind, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FFLogsIntegrationException exception)
        {
            return FFLogsApiResult<JsonElement>.Failure(exception.Error);
        }

        var payload = JsonSerializer.Serialize(new { query, variables });
        using var request = new HttpRequestMessage(HttpMethod.Post, options.GetGraphQlEndpoint(accessKind))
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.RevealForAuthorizationHeader());

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return FFLogsApiResult<JsonElement>.Failure(FFLogsIntegrationErrors.NetworkFailure(operation));
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return FFLogsApiResult<JsonElement>.Failure(MapApiHttpError(response, operation));
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                var root = document.RootElement;
                if (root.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind == JsonValueKind.Array &&
                    errors.GetArrayLength() > 0)
                {
                    return FFLogsApiResult<JsonElement>.Failure(MapGraphQlErrors(errors, operation));
                }

                if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                {
                    return FFLogsApiResult<JsonElement>.Failure(FFLogsIntegrationErrors.ProtocolFailure(operation));
                }

                return FFLogsApiResult<JsonElement>.Success(data.Clone());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (JsonException)
            {
                return FFLogsApiResult<JsonElement>.Failure(FFLogsIntegrationErrors.ProtocolFailure(operation));
            }
        }
    }

    private static FFLogsReportDocument? ParseReportDocument(JsonElement data)
    {
        if (!TryGetReport(data, out var report))
        {
            return null;
        }

        var metadata = new FFLogsReportMetadata
        {
            Code = RequireString(report, "code"),
            StartTimeUnixMilliseconds = RequireDouble(report, "startTime"),
            EndTimeUnixMilliseconds = RequireDouble(report, "endTime"),
            Revision = RequireInt32(report, "revision"),
        };

        var fights = new List<FFLogsFightMetadata>();
        if (report.TryGetProperty("fights", out var fightsElement) && fightsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var fight in fightsElement.EnumerateArray())
            {
                var gameZoneId = default(double?);
                var gameZoneName = default(string);
                if (fight.TryGetProperty("gameZone", out var gameZone) && gameZone.ValueKind == JsonValueKind.Object)
                {
                    gameZoneId = OptionalDouble(gameZone, "id");
                    gameZoneName = OptionalString(gameZone, "name");
                }

                fights.Add(new FFLogsFightMetadata
                {
                    Id = RequireInt32(fight, "id"),
                    EncounterId = RequireInt32(fight, "encounterID"),
                    Name = RequireString(fight, "name"),
                    StartTimeMilliseconds = RequireDouble(fight, "startTime"),
                    EndTimeMilliseconds = RequireDouble(fight, "endTime"),
                    Kill = OptionalBoolean(fight, "kill"),
                    InProgress = OptionalBoolean(fight, "inProgress"),
                    GameZoneId = gameZoneId,
                    GameZoneName = gameZoneName,
                });
            }
        }

        return new FFLogsReportDocument
        {
            Report = metadata,
            Fights = fights,
        };
    }

    private static FFLogsEventPage? ParseEventPage(JsonElement data)
    {
        if (!TryGetReport(data, out var report))
        {
            return null;
        }

        if (!report.TryGetProperty("events", out var paginator) || paginator.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new FFLogsEventPage
            {
                Events = Array.Empty<FFLogsEventEnvelope>(),
                NextPageTimestampMilliseconds = null,
            };
        }

        if (paginator.ValueKind != JsonValueKind.Object ||
            !paginator.TryGetProperty("data", out var eventsData) ||
            eventsData.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("FFLogs event paginator data was not an array.");
        }

        var events = new List<FFLogsEventEnvelope>(eventsData.GetArrayLength());
        foreach (var evt in eventsData.EnumerateArray())
        {
            if (evt.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("FFLogs event payload was not an object.");
            }

            events.Add(new FFLogsEventEnvelope
            {
                TimestampMilliseconds = RequireDouble(evt, "timestamp"),
                Type = RequireString(evt, "type"),
                Payload = evt.Clone(),
            });
        }

        return new FFLogsEventPage
        {
            Events = events,
            NextPageTimestampMilliseconds = OptionalDouble(paginator, "nextPageTimestamp"),
        };
    }

    private static bool TryGetReport(JsonElement data, out JsonElement report)
    {
        report = default;
        return data.TryGetProperty("reportData", out var reportData) &&
            reportData.ValueKind == JsonValueKind.Object &&
            reportData.TryGetProperty("report", out report) &&
            report.ValueKind == JsonValueKind.Object;
    }

    private static PullImportError MapApiHttpError(HttpResponseMessage response, FFLogsOperation operation)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return FFLogsIntegrationErrors.AuthenticationFailed();
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return FFLogsIntegrationErrors.PrivateReportUnavailable();
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return FFLogsIntegrationErrors.ReportNotFound();
        }

        if ((int)response.StatusCode == 429)
        {
            return FFLogsIntegrationErrors.RateLimited(GetRetryAfter(response));
        }

        if ((int)response.StatusCode >= 500)
        {
            return FFLogsIntegrationErrors.Unavailable();
        }

        return FFLogsIntegrationErrors.ProtocolFailure(operation);
    }

    private static PullImportError MapGraphQlErrors(JsonElement errors, FFLogsOperation operation)
    {
        foreach (var error in errors.EnumerateArray())
        {
            if (!error.TryGetProperty("message", out var messageElement) || messageElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var message = messageElement.GetString() ?? string.Empty;
            if (ContainsAny(message, "private", "permission", "forbidden", "unauthorized", "not authorized", "access denied"))
            {
                return FFLogsIntegrationErrors.PrivateReportUnavailable();
            }

            if (ContainsAny(message, "not found", "does not exist", "unknown report"))
            {
                return FFLogsIntegrationErrors.ReportNotFound();
            }
        }

        return FFLogsIntegrationErrors.ProtocolFailure(operation);
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }

        return null;
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Required FFLogs property '{propertyName}' was missing or not a string.");
        }

        return property.GetString() ?? throw new InvalidOperationException($"Required FFLogs property '{propertyName}' was null.");
    }

    private static double RequireDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetDouble(out var value) ||
            !double.IsFinite(value))
        {
            throw new InvalidOperationException($"Required FFLogs property '{propertyName}' was missing or not a finite number.");
        }

        return value;
    }

    private static int RequireInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out var value))
        {
            throw new InvalidOperationException($"Required FFLogs property '{propertyName}' was missing or not an integer.");
        }

        return value;
    }

    private static string? OptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static double? OptionalDouble(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out var value) &&
            double.IsFinite(value)
            ? value
            : null;
    }

    private static bool? OptionalBoolean(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;
    }
}
