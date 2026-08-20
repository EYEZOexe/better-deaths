namespace BetterDeaths;

using BetterDeaths.Domain;
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
    public async Task ReportMetadataParsesMasterActorsAndAbilitiesAndUsesShortLivedCredentialFreeCache()
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

        Assert.Equal(3, first.Value.Actors.Count);
        var player = Assert.Single(first.Value.Actors, actor => actor.Id == 10);
        var pet = Assert.Single(first.Value.Actors, actor => actor.Id == 20);
        var boss = Assert.Single(first.Value.Actors, actor => actor.Id == 30);
        Assert.Equal("Player One", player.Name);
        Assert.Equal("Player", player.Type);
        Assert.Equal("Dancer", player.SubType);
        Assert.Equal(10, pet.PetOwnerId);
        Assert.Equal("NPC", boss.Type);
        Assert.Equal("Boss", boss.SubType);

        Assert.Equal(5, first.Value.Abilities.Count);
        var devilment = Assert.Single(first.Value.Abilities, ability => ability.GameId == 1_001_825);
        Assert.Equal("Devilment", devilment.Name);
        Assert.Equal("ability_dancer_devilment.png", devilment.Icon);
        Assert.Equal("Dancer", devilment.Type);

        Assert.Single(handler.Requests);
        Assert.Equal("https://www.fflogs.com/api/v2/client", handler.Requests[0].Uri.ToString());
        Assert.Equal("Bearer", handler.Requests[0].AuthorizationScheme);
        Assert.Contains("AnalyzerReportMetadata", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("masterData(translate: false)", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("petOwner", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("abilities", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("gameID", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("icon", handler.Requests[0].Body, StringComparison.Ordinal);

        var cacheKey = FFLogsReportCacheKey.Create("REPORT123", FFLogsApiAccessKind.PublicClient);
        Assert.DoesNotContain("REPORT123", cacheKey.ReportHash, StringComparison.Ordinal);
        Assert.DoesNotContain("TEST_ACCESS_TOKEN", cacheKey.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FightImportFollowsStrictlyAdvancingPaginationAndCarriesMasterActorsByRevision()
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

        var first = await client.LoadFightAsync(
            "REPORT123",
            42,
            FFLogsApiAccessKind.PublicClient,
            FFLogsImportProfile.Core);
        var second = await client.LoadFightAsync(
            "REPORT123",
            42,
            FFLogsApiAccessKind.PublicClient,
            FFLogsImportProfile.Core);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, first.Value?.Events.Count);
        Assert.Equal(3, first.Value?.Actors.Count);
        Assert.Equal(5, first.Value?.ReportDocument.Abilities.Count);
        Assert.Equal(new[] { "damage", "heal" }, first.Value!.Events.Select(evt => evt.Type));
        Assert.Equal(new[] { 1100d, 1600d }, first.Value.Events.Select(evt => evt.TimestampMilliseconds));
        Assert.Equal(3, handler.Requests.Count);

        using var pageOneRequest = JsonDocument.Parse(handler.Requests[1].Body);
        using var pageTwoRequest = JsonDocument.Parse(handler.Requests[2].Body);
        Assert.Equal(1000d, pageOneRequest.RootElement.GetProperty("variables").GetProperty("startTime").GetDouble());
        Assert.Equal(1500d, pageTwoRequest.RootElement.GetProperty("variables").GetProperty("startTime").GetDouble());
        Assert.Equal(10000, pageTwoRequest.RootElement.GetProperty("variables").GetProperty("limit").GetInt32());
        Assert.False(pageOneRequest.RootElement.GetProperty("variables").GetProperty("includeResources").GetBoolean());
        Assert.False(pageTwoRequest.RootElement.GetProperty("variables").GetProperty("includeResources").GetBoolean());
        Assert.Contains(
            "includeResources: $includeResources",
            pageTwoRequest.RootElement.GetProperty("query").GetString(),
            StringComparison.Ordinal);
        Assert.Contains("useActorIDs: true", pageTwoRequest.RootElement.GetProperty("query").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeepAnalysisRequestsResourcesAndDoesNotReuseCoreEventPages()
    {
        var handler = new QueueHttpMessageHandler(
            ReportResponse(),
            EventResponse(
                """[{"timestamp":1100,"type":"damage","sourceID":30,"targetID":10,"amount":100}]""",
                nextPageTimestamp: null),
            EventResponse(
                """[{"timestamp":1100,"type":"damage","sourceID":30,"targetID":10,"amount":100,"targetResources":{"hitPoints":900,"maxHitPoints":1000}}]""",
                nextPageTimestamp: null));
        using var httpClient = new HttpClient(handler);
        var client = Client(httpClient);

        var core = await client.LoadFightAsync(
            "REPORT123",
            42,
            FFLogsApiAccessKind.PublicClient,
            FFLogsImportProfile.Core);
        var cachedCore = await client.LoadFightAsync(
            "REPORT123",
            42,
            FFLogsApiAccessKind.PublicClient,
            FFLogsImportProfile.Core);
        var deep = await client.LoadFightAsync(
            "REPORT123",
            42,
            FFLogsApiAccessKind.PublicClient,
            FFLogsImportProfile.DeepAnalysis);
        var cachedDeep = await client.LoadFightAsync(
            "REPORT123",
            42,
            FFLogsApiAccessKind.PublicClient,
            FFLogsImportProfile.DeepAnalysis);

        Assert.True(core.IsSuccess);
        Assert.True(cachedCore.IsSuccess);
        Assert.True(deep.IsSuccess);
        Assert.True(cachedDeep.IsSuccess);
        Assert.Equal(FFLogsImportProfile.Core, core.Value!.Profile);
        Assert.Equal(FFLogsImportProfile.Core, cachedCore.Value!.Profile);
        Assert.Equal(FFLogsImportProfile.DeepAnalysis, deep.Value!.Profile);
        Assert.Equal(FFLogsImportProfile.DeepAnalysis, cachedDeep.Value!.Profile);
        Assert.False(Assert.Single(core.Value!.Events).Payload.TryGetProperty("targetResources", out _));
        Assert.True(Assert.Single(deep.Value!.Events).Payload.TryGetProperty("targetResources", out _));
        Assert.Equal(3, handler.Requests.Count);

        using var coreRequest = JsonDocument.Parse(handler.Requests[1].Body);
        using var deepRequest = JsonDocument.Parse(handler.Requests[2].Body);
        Assert.False(coreRequest.RootElement.GetProperty("variables").GetProperty("includeResources").GetBoolean());
        Assert.True(deepRequest.RootElement.GetProperty("variables").GetProperty("includeResources").GetBoolean());
    }

    [Fact]
    public async Task PullSourceUsesItsExplicitDeepAnalysisProfile()
    {
        var handler = new QueueHttpMessageHandler(
            ReportResponse(),
            EventResponse(
                """[{"timestamp":1100,"type":"damage","sourceID":30,"targetID":10,"amount":100,"targetResources":{"hitPoints":900,"maxHitPoints":1000}}]""",
                nextPageTimestamp: null));
        using var httpClient = new HttpClient(handler);
        var source = new FFLogsPullSource(
            Client(httpClient),
            new PullSchemaVersion(1),
            FFLogsApiAccessKind.PublicClient,
            FFLogsImportProfile.DeepAnalysis);

        var result = await source.LoadPullAsync("REPORT123", 42);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Pull!.Events);
        using var request = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.True(request.RootElement.GetProperty("variables").GetProperty("includeResources").GetBoolean());
    }

    [Fact]
    public void EventPageCacheIdentityIncludesProfileAccessRevisionAndPageCoordinatesWithoutSecrets()
    {
        var baseline = FFLogsEventPageCacheKey.Create(
            "REPORT123",
            FFLogsApiAccessKind.PublicClient,
            FFLogsImportProfile.Core,
            revision: 7,
            fightId: 42,
            startTimeMilliseconds: 1000,
            endTimeMilliseconds: 3000,
            limit: 10000);

        Assert.NotEqual(baseline, baseline with { Profile = FFLogsImportProfile.DeepAnalysis });
        Assert.NotEqual(baseline, baseline with { AccessKind = FFLogsApiAccessKind.UserAuthorized });
        Assert.NotEqual(baseline, baseline with { Revision = 8 });
        Assert.NotEqual(baseline, baseline with { FightId = 43 });
        Assert.NotEqual(baseline, baseline with { StartTimeMilliseconds = 1500 });
        Assert.NotEqual(baseline, baseline with { EndTimeMilliseconds = 3500 });
        Assert.NotEqual(baseline, baseline with { Limit = 5000 });
        Assert.Contains(nameof(FFLogsImportProfile.Core), baseline.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("REPORT123", baseline.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("TEST_ACCESS_TOKEN", baseline.ToString(), StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => FFLogsEventPageCacheKey.Create(
            "REPORT123",
            FFLogsApiAccessKind.PublicClient,
            (FFLogsImportProfile)999,
            revision: 7,
            fightId: 42,
            startTimeMilliseconds: 1000,
            endTimeMilliseconds: 3000,
            limit: 10000));
    }

    [Fact]
    public async Task InvalidImportProfileFailsBeforeAnyRequest()
    {
        var handler = new QueueHttpMessageHandler();
        using var httpClient = new HttpClient(handler);

        var result = await Client(httpClient).LoadFightAsync(
            "REPORT123",
            42,
            FFLogsApiAccessKind.PublicClient,
            (FFLogsImportProfile)999);

        Assert.False(result.IsSuccess);
        Assert.Equal(PullImportErrorCategory.InvalidRequest, result.Error?.Category);
        Assert.Empty(handler.Requests);

        Assert.Throws<ArgumentOutOfRangeException>(() => new FFLogsPullSource(
            Client(httpClient),
            new PullSchemaVersion(1),
            FFLogsApiAccessKind.PublicClient,
            (FFLogsImportProfile)999));
    }

    [Fact]
    public async Task MetadataExpiryRefreshesAbilityCatalogAndRevisionSeparatesEventPageCache()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-08-20T00:00:00Z"));
        var handler = new QueueHttpMessageHandler(
            ReportResponse(
                revision: 7,
                abilitiesJson: """[{"gameID":1001825,"name":"Devilment","icon":"devilment.png","type":"Dancer"}]"""),
            EventResponse(
                """[{"timestamp":1100,"type":"applybuff","sourceID":10,"targetID":10,"abilityGameID":1001825,"duration":20000}]""",
                nextPageTimestamp: null),
            ReportResponse(
                revision: 8,
                abilitiesJson: """[{"gameID":1005084,"name":"Forsaken Stack","icon":"forsaken.png","type":"Encounter"}]"""),
            EventResponse(
                """[{"timestamp":1200,"type":"applydebuff","sourceID":30,"targetID":10,"abilityGameID":1005084,"duration":9999000}]""",
                nextPageTimestamp: null));
        using var httpClient = new HttpClient(handler);
        var client = new FFLogsGraphQlClient(
            httpClient,
            new FixedTokenProvider(),
            cache: new MemoryFFLogsImportCache(),
            timeProvider: clock);

        var revisionSeven = await client.LoadFightAsync(
            "REPORT123",
            42,
            FFLogsApiAccessKind.PublicClient,
            FFLogsImportProfile.Core);
        var cachedRevisionSeven = await client.LoadFightAsync(
            "REPORT123",
            42,
            FFLogsApiAccessKind.PublicClient,
            FFLogsImportProfile.Core);
        clock.Advance(TimeSpan.FromMinutes(3));
        var revisionEight = await client.LoadFightAsync(
            "REPORT123",
            42,
            FFLogsApiAccessKind.PublicClient,
            FFLogsImportProfile.Core);

        Assert.True(revisionSeven.IsSuccess);
        Assert.True(cachedRevisionSeven.IsSuccess);
        Assert.True(revisionEight.IsSuccess);
        Assert.Equal(FFLogsImportProfile.Core, revisionSeven.Value!.Profile);
        Assert.Equal(FFLogsImportProfile.Core, cachedRevisionSeven.Value!.Profile);
        Assert.Equal(FFLogsImportProfile.Core, revisionEight.Value!.Profile);
        Assert.Equal(7, revisionSeven.Value?.ReportDocument.Report.Revision);
        Assert.Equal((uint)1_001_825, Assert.Single(revisionSeven.Value!.ReportDocument.Abilities).GameId);
        Assert.Equal(8, revisionEight.Value?.ReportDocument.Report.Revision);
        Assert.Equal((uint)1_005_084, Assert.Single(revisionEight.Value!.ReportDocument.Abilities).GameId);
        Assert.Equal("applybuff", Assert.Single(revisionSeven.Value.Events).Type);
        Assert.Equal("applydebuff", Assert.Single(revisionEight.Value.Events).Type);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task MalformedAbilityIdsAreIgnoredWithoutDiscardingValidCatalogEntries()
    {
        var handler = new QueueHttpMessageHandler(ReportResponse(
            abilitiesJson: """
                [
                  {"gameID":1001825,"name":"Devilment","icon":"devilment.png","type":"Dancer"},
                  {"gameID":1.5,"name":"Fractional","icon":"bad.png","type":"Bad"},
                  {"gameID":-1,"name":"Negative","icon":"bad.png","type":"Bad"},
                  {"gameID":4294967296,"name":"Too Large","icon":"bad.png","type":"Bad"},
                  {"gameID":null,"name":"Missing","icon":"bad.png","type":"Bad"}
                ]
                """));
        using var httpClient = new HttpClient(handler);

        var result = await Client(httpClient).LoadReportAsync("REPORT123", FFLogsApiAccessKind.PublicClient);

        Assert.True(result.IsSuccess);
        Assert.Equal((uint)1_001_825, Assert.Single(result.Value!.Abilities).GameId);
    }

    [Fact]
    public async Task NonAdvancingPaginationCursorFailsAsProtocolError()
    {
        var handler = new QueueHttpMessageHandler(
            ReportResponse(),
            EventResponse("[]", nextPageTimestamp: 1000));
        using var httpClient = new HttpClient(handler);
        var client = Client(httpClient);

        var result = await client.LoadFightAsync(
            "REPORT123",
            42,
            FFLogsApiAccessKind.PublicClient,
            FFLogsImportProfile.Core);

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

    private static HttpResponseMessage ReportResponse(
        int revision = 7,
        string? abilitiesJson = null)
    {
        abilitiesJson ??= """
            [
              { "gameID": 1001825, "name": "Devilment", "icon": "ability_dancer_devilment.png", "type": "Dancer" },
              { "gameID": 1005084, "name": "Forsaken Stack", "icon": "forsaken_stack.png", "type": "Encounter" },
              { "gameID": 1005085, "name": "Forsaken Spread", "icon": "forsaken_spread.png", "type": "Encounter" },
              { "gameID": 1005086, "name": "Forsaken Cone", "icon": "forsaken_cone.png", "type": "Encounter" },
              { "gameID": 15997, "name": "Standard Step", "icon": "ability_dancer_standardstep.png", "type": "Dancer" }
            ]
            """;
        return Json(HttpStatusCode.OK, $$"""
            {
              "data": {
                "reportData": {
                  "report": {
                    "code": "REPORT123",
                    "startTime": 100000,
                    "endTime": 200000,
                    "revision": {{revision}},
                    "masterData": {
                      "actors": [
                        { "id": 10, "name": "Player One", "type": "Player", "subType": "Dancer", "petOwner": null },
                        { "id": 20, "name": "Pet One", "type": "Pet", "subType": "Pet", "petOwner": 10 },
                        { "id": 30, "name": "Test Boss", "type": "NPC", "subType": "Boss", "petOwner": null }
                      ],
                      "abilities": {{abilitiesJson}}
                    },
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

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration)
        {
            utcNow += duration;
        }
    }
}
