namespace BetterDeaths;

using BetterDeaths.Sources;
using BetterDeaths.Sources.FFLogs;
using BetterDeaths.Sources.FFLogs.Client;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public sealed class FFLogsClientTests
{
    [Fact]
    public async Task ClientCredentialsProviderPostsOAuthGrantAndCachesToken()
    {
        var handler = new QueueHttpMessageHandler(
            Json(HttpStatusCode.OK, """{"access_token":"TEST_ACCESS_TOKEN","token_type":"Bearer","expires_in":3600}"""));
        using var httpClient = new HttpClient(handler);
        using var provider = new FFLogsClientCredentialsTokenProvider(
            httpClient,
            new Uri("https://example.invalid/oauth/token"),
            new FFLogsClientCredentials("TEST_CLIENT_ID", "NOT_A_REAL_SECRET"));

        var first = await provider.GetAccessTokenAsync(FFLogsApiAccessKind.PublicClient);
        var second = await provider.GetAccessTokenAsync(FFLogsApiAccessKind.PublicClient);

        Assert.Equal("TEST_ACCESS_TOKEN", first.RevealForAuthorizationHeader());
        Assert.Same(first, second);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://example.invalid/oauth/token", request.Uri.ToString());
        Assert.Equal("Basic", request.AuthorizationScheme);
        Assert.NotNull(request.AuthorizationParameter);
        var basic = Encoding.UTF8.GetString(Convert.FromBase64String(request.AuthorizationParameter!));
        Assert.Equal("TEST_CLIENT_ID:NOT_A_REAL_SECRET", basic);
        Assert.Contains("grant_type=client_credentials", request.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT_A_REAL_SECRET", provider.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClientCredentialsProviderRejectsUserAuthorizedModeWithoutLeakingCredentials()
    {
        using var httpClient = new HttpClient(new QueueHttpMessageHandler());
        using var provider = new FFLogsClientCredentialsTokenProvider(
            httpClient,
            new Uri("https://example.invalid/oauth/token"),
            new FFLogsClientCredentials("TEST_CLIENT_ID", "NOT_A_REAL_SECRET"));

        var error = await Assert.ThrowsAsync<FFLogsIntegrationException>(async () =>
            await provider.GetAccessTokenAsync(FFLogsApiAccessKind.UserAuthorized));

        Assert.Equal(PullImportErrorCategory.Authorization, error.Error.Category);
        Assert.DoesNotContain("TEST_CLIENT_ID", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT_A_REAL_SECRET", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportMetadataParsesAndUsesShortLivedCredentialFreeCache()
    {
        var handler = new QueueHttpMessageHandler(ReportResponse());
        using var httpClient = new HttpClient(handler);
        var client = Client(httpClient);

        var first = await client.LoadReportAsync("REPORT123", FFLogsApiAccessKind.PublicClient);
        var second = await client.LoadReportAsync("REPORT123", FFLogsApiAccessKind.PublicClient);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("REPORT123", first.Value?.Report.Code);
        Assert.Equal(7, first.Value?.Report.Revision);
        var fight = Assert.Single(first.Value!.Fights);
        Assert.Equal(42, fight.Id);
        Assert.Equal(1234, fight.EncounterId);
        Assert.Equal(777d, fight.GameZoneId);
        Assert.Equal("Test Zone", fight.GameZoneName);
        Assert.Single(handler.Requests);
        Assert.Equal("https://www.fflogs.com/api/v2/client", handler.Requests[0].Uri.ToString());
        Assert.Equal("Bearer", handler.Requests[0].AuthorizationScheme);
        Assert.Contains("AnalyzerReportMetadata", handler.Requests[0].Body, StringComparison.Ordinal);

        var cacheKey = FFLogsReportCacheKey.Create("REPORT123", FFLogsApiAccessKind.PublicClient);
        Assert.DoesNotContain("REPORT123", cacheKey.ReportHash, StringComparison.Ordinal);
        Assert.DoesNotContain("TEST_ACCESS_TOKEN", cacheKey.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FightImportFollowsStrictlyAdvancingPaginationAndCachesPagesByRevision()
    {
        var handler = new QueueHttpMessageHandler(
            ReportResponse(),
            EventResponse(
                """[{"timestamp":1100,"type":"damage","sourceID":1,"targetID":2}]""",
                nextPageTimestamp: 1500),
            EventResponse(
                """[{"timestamp":1600,"type":"heal","sourceID":2,"targetID":1}]""",
                nextPageTimestamp: null));
        using var httpClient = new HttpClient(handler);
        var client = Client(httpClient);

        var first = await client.LoadFightAsync("REPORT123", 42, FFLogsApiAccessKind.PublicClient);
        var second = await client.LoadFightAsync("REPORT123", 42, FFLogsApiAccessKind.PublicClient);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, first.Value?.Events.Count);
        Assert.Equal(new[] { "damage", "heal" }, first.Value!.Events.Select(evt => evt.Type));
        Assert.Equal(new[] { 1100d, 1600d }, first.Value.Events.Select(evt => evt.TimestampMilliseconds));
        Assert.Equal(3, handler.Requests.Count);

        using var pageOneRequest = JsonDocument.Parse(handler.Requests[1].Body);
        using var pageTwoRequest = JsonDocument.Parse(handler.Requests[2].Body);
        Assert.Equal(1000d, pageOneRequest.RootElement.GetProperty("variables").GetProperty("startTime").GetDouble());
        Assert.Equal(1500d, pageTwoRequest.RootElement.GetProperty("variables").GetProperty("startTime").GetDouble());
        Assert.Equal(10000, pageTwoRequest.RootElement.GetProperty("variables").GetProperty("limit").GetInt32());
        Assert.Contains("useActorIDs: true", pageTwoRequest.RootElement.GetProperty("query").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonAdvancingPaginationCursorFailsAsProtocolError()
    {
        var handler = new QueueHttpMessageHandler(
            ReportResponse(),
            EventResponse("[]", nextPageTimestamp: 1000));
        using var httpClient = new HttpClient(handler);
        var client = Client(httpClient);

        var result = await client.LoadFightAsync("REPORT123", 42, FFLogsApiAccessKind.PublicClient);

        Assert.False(result.IsSuccess);
        Assert.Equal(PullImportErrorCategory.Protocol, result.Error?.Category);
        Assert.Equal("fflogs.protocol.load_fight_events", result.Error?.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, PullImportErrorCategory.Authentication)]
    [InlineData(HttpStatusCode.Forbidden, PullImportErrorCategory.Authorization)]
    [InlineData(HttpStatusCode.NotFound, PullImportErrorCategory.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError, PullImportErrorCategory.Unavailable)]
    public async Task HttpFailuresMapToSafeIntegrationErrors(HttpStatusCode status, PullImportErrorCategory expected)
    {
        var handler = new QueueHttpMessageHandler(new HttpResponseMessage(status));
        using var httpClient = new HttpClient(handler);
        var client = Client(httpClient);

        var result = await client.LoadReportAsync("REPORT123", FFLogsApiAccessKind.PublicClient);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error?.Category);
        Assert.DoesNotContain("TEST_ACCESS_TOKEN", result.Error?.SafeMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RateLimitRetryAfterIsStructuredWithoutRawResponseText()
    {
        var response = new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("raw service text TEST_ACCESS_TOKEN"),
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(45));
        var handler = new QueueHttpMessageHandler(response);
        using var httpClient = new HttpClient(handler);
        var client = Client(httpClient);

        var result = await client.LoadReportAsync("REPORT123", FFLogsApiAccessKind.PublicClient);

        Assert.Equal(PullImportErrorCategory.RateLimited, result.Error?.Category);
        Assert.Equal(TimeSpan.FromSeconds(45), result.Error?.RetryAfter);
        Assert.DoesNotContain("TEST_ACCESS_TOKEN", result.Error?.SafeMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GraphQlPermissionErrorMapsToAuthorizationWithoutReturningServerMessage()
    {
        var handler = new QueueHttpMessageHandler(Json(
            HttpStatusCode.OK,
            """{"errors":[{"message":"Private report access denied TEST_ACCESS_TOKEN"}],"data":null}"""));
        using var httpClient = new HttpClient(handler);
        var client = Client(httpClient);

        var result = await client.LoadReportAsync("REPORT123", FFLogsApiAccessKind.PublicClient);

        Assert.Equal(PullImportErrorCategory.Authorization, result.Error?.Category);
        Assert.DoesNotContain("TEST_ACCESS_TOKEN", result.Error?.SafeMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NetworkFailureAndCancellationStayDistinct()
    {
        using var networkClient = new HttpClient(new ThrowingHttpMessageHandler(new HttpRequestException("TEST_ACCESS_TOKEN")));
        var networkResult = await Client(networkClient).LoadReportAsync("REPORT123", FFLogsApiAccessKind.PublicClient);
        Assert.Equal(PullImportErrorCategory.Network, networkResult.Error?.Category);
        Assert.DoesNotContain("TEST_ACCESS_TOKEN", networkResult.Error?.SafeMessage ?? string.Empty, StringComparison.Ordinal);

        using var cancellationClient = new HttpClient(new QueueHttpMessageHandler(ReportResponse()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await Client(cancellationClient).LoadReportAsync("REPORT123", FFLogsApiAccessKind.PublicClient, cancellation.Token));
    }

    [Fact]
    public void OptionsEnforceOfficialEventPageLimitRangeAndHttpsEndpoints()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FFLogsApiClientOptions { EventPageLimit = 99 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new FFLogsApiClientOptions { EventPageLimit = 10001 }.Validate());
        Assert.Throws<ArgumentException>(() => new FFLogsApiClientOptions
        {
            PublicGraphQlEndpoint = new Uri("http://example.invalid/api"),
        }.Validate());
    }

    private static FFLogsGraphQlClient Client(HttpClient httpClient)
    {
        return new FFLogsGraphQlClient(
            httpClient,
            new FixedTokenProvider(),
            cache: new MemoryFFLogsImportCache());
    }

    private static HttpResponseMessage ReportResponse()
    {
        return Json(HttpStatusCode.OK, """
            {
              "data": {
                "reportData": {
                  "report": {
                    "code": "REPORT123",
                    "startTime": 100000,
                    "endTime": 200000,
                    "revision": 7,
                    "fights": [
                      {
                        "id": 42,
                        "encounterID": 1234,
                        "name": "Test Encounter",
                        "startTime": 1000,
                        "endTime": 3000,
                        "kill": false,
                        "inProgress": false,
                        "gameZone": { "id": 777, "name": "Test Zone" }
                      }
                    ]
                  }
                }
              }
            }
            """);
    }

    private static HttpResponseMessage EventResponse(string dataJson, double? nextPageTimestamp)
    {
        var next = nextPageTimestamp is { } value
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "null";
        return Json(HttpStatusCode.OK, $$"""
            {
              "data": {
                "reportData": {
                  "report": {
                    "events": {
                      "data": {{dataJson}},
                      "nextPageTimestamp": {{next}}
                    }
                  }
                }
              }
            }
            """);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class FixedTokenProvider : IFFLogsAccessTokenProvider
    {
        public ValueTask<FFLogsAccessToken> GetAccessTokenAsync(
            FFLogsApiAccessKind accessKind,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new FFLogsAccessToken("TEST_ACCESS_TOKEN"));
        }
    }

    private sealed record RequestSnapshot(
        HttpMethod Method,
        Uri Uri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string Body);

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses;

        public QueueHttpMessageHandler(params HttpResponseMessage[] responses)
        {
            this.responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<RequestSnapshot> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestSnapshot(
                request.Method,
                request.RequestUri ?? throw new InvalidOperationException("Request URI missing."),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                body));

            if (responses.Count == 0)
            {
                throw new InvalidOperationException("No queued HTTP response is available for this test request.");
            }

            return responses.Dequeue();
        }
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromException<HttpResponseMessage>(exception);
        }
    }
}
