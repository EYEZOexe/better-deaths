namespace BetterDeaths;

using BetterDeaths.Sources;
using BetterDeaths.Sources.FFLogs;
using BetterDeaths.Sources.FFLogs.Client;
using System.Net;
using System.Text;

public sealed class FFLogsTokenProviderEdgeTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("\"not-a-number\"")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task MalformedExpiresInProducesSafeProtocolError(string expiresInJson)
    {
        const string token = "TEST_ACCESS_TOKEN_SHOULD_NOT_LEAK";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{{\"access_token\":\"{token}\",\"expires_in\":{expiresInJson}}}",
                Encoding.UTF8,
                "application/json"),
        };
        using var httpClient = new HttpClient(new SingleResponseHandler(response));
        using var provider = new FFLogsClientCredentialsTokenProvider(
            httpClient,
            new Uri("https://example.invalid/oauth/token"),
            new FFLogsClientCredentials("TEST_CLIENT_ID", "NOT_A_REAL_SECRET"));

        var exception = await Assert.ThrowsAsync<FFLogsIntegrationException>(async () =>
            await provider.GetAccessTokenAsync(FFLogsApiAccessKind.PublicClient));

        Assert.Equal(PullImportErrorCategory.Protocol, exception.Error.Category);
        Assert.Equal("fflogs.protocol.authenticate", exception.Error.Code);
        Assert.DoesNotContain(token, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT_A_REAL_SECRET", exception.Message, StringComparison.Ordinal);
    }

    private sealed class SingleResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        private bool used;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (used)
            {
                throw new InvalidOperationException("Test handler received more than one request.");
            }

            used = true;
            return Task.FromResult(response);
        }
    }
}
