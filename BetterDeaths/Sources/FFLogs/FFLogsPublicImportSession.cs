namespace BetterDeaths.Sources.FFLogs;

using BetterDeaths.Domain;
using BetterDeaths.Sources;
using BetterDeaths.Sources.FFLogs.Client;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

internal sealed class FFLogsPublicImportSession : IFFLogsImportSource, IDisposable
{
    private static readonly Uri TokenEndpoint = new("https://www.fflogs.com/oauth/token");

    private readonly HttpClient httpClient;
    private readonly FFLogsClientCredentialsTokenProvider tokenProvider;
    private readonly FFLogsPullSource source;
    private bool disposed;

    private FFLogsPublicImportSession(
        HttpClient httpClient,
        FFLogsClientCredentialsTokenProvider tokenProvider,
        FFLogsPullSource source)
    {
        this.httpClient = httpClient;
        this.tokenProvider = tokenProvider;
        this.source = source;
    }

    public static FFLogsPublicImportSession Create(
        string clientId,
        string clientSecret,
        PullSchemaVersion schemaVersion)
    {
        var normalizedClientId = FFLogsCredentialInput.NormalizeClientId(clientId);
        var normalizedClientSecret = FFLogsCredentialInput.NormalizeClientSecret(clientSecret);
        var credentials = new FFLogsClientCredentials(normalizedClientId, normalizedClientSecret);
        var httpClient = new HttpClient();
        var tokenProvider = new FFLogsClientCredentialsTokenProvider(
            httpClient,
            TokenEndpoint,
            credentials);
        var client = new FFLogsGraphQlClient(httpClient, tokenProvider);
        return new FFLogsPublicImportSession(
            httpClient,
            tokenProvider,
            new FFLogsPullSource(client, schemaVersion));
    }

    public ValueTask<FFLogsReportSelectionResult> LoadReportSelectionAsync(
        string reportCode,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return source.LoadReportSelectionAsync(reportCode, cancellationToken);
    }

    public ValueTask<PullImportResult> LoadPullAsync(
        string reportCode,
        int fightId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return source.LoadPullAsync(reportCode, fightId, cancellationToken);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        tokenProvider.Dispose();
        httpClient.Dispose();
    }
}
