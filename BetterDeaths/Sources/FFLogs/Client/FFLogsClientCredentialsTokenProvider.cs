namespace BetterDeaths.Sources.FFLogs.Client;

using BetterDeaths.Sources;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal sealed class FFLogsClientCredentials
{
    private readonly string clientId;
    private readonly string clientSecret;

    public FFLogsClientCredentials(string clientId, string clientSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        this.clientId = clientId;
        this.clientSecret = clientSecret;
    }

    internal string CreateBasicAuthenticationParameter()
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
    }

    public override string ToString()
    {
        return "[REDACTED FFLogs client credentials]";
    }
}

internal sealed class FFLogsClientCredentialsTokenProvider : IFFLogsAccessTokenProvider, IDisposable
{
    private static readonly TimeSpan ExpirySafetyMargin = TimeSpan.FromSeconds(30);

    private readonly HttpClient httpClient;
    private readonly Uri tokenEndpoint;
    private readonly FFLogsClientCredentials credentials;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim refreshGate = new(1, 1);

    private CachedAccessToken? cached;
    private bool disposed;

    public FFLogsClientCredentialsTokenProvider(
        HttpClient httpClient,
        Uri tokenEndpoint,
        FFLogsClientCredentials credentials,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokenEndpoint);
        ArgumentNullException.ThrowIfNull(credentials);
        if (!tokenEndpoint.IsAbsoluteUri || tokenEndpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("FFLogs OAuth token endpoint must be an absolute HTTPS URI.", nameof(tokenEndpoint));
        }

        this.httpClient = httpClient;
        this.tokenEndpoint = tokenEndpoint;
        this.credentials = credentials;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<FFLogsAccessToken> GetAccessTokenAsync(
        FFLogsApiAccessKind accessKind,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (accessKind != FFLogsApiAccessKind.PublicClient)
        {
            throw new FFLogsIntegrationException(new PullImportError
            {
                Category = PullImportErrorCategory.Authorization,
                Code = "fflogs.user_authorization_provider_required",
                SafeMessage = "Private FFLogs access requires a user-authorized token provider.",
            });
        }

        var now = timeProvider.GetUtcNow();
        if (cached is { } existing && IsUsable(existing, now))
        {
            return existing.Token;
        }

        await refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = timeProvider.GetUtcNow();
            if (cached is { } refreshed && IsUsable(refreshed, now))
            {
                return refreshed.Token;
            }

            cached = await RequestAccessTokenAsync(now, cancellationToken).ConfigureAwait(false);
            return cached.Token;
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        refreshGate.Dispose();
    }

    private async Task<CachedAccessToken> RequestAccessTokenAsync(
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            credentials.CreateBasicAuthenticationParameter());

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
            throw new FFLogsIntegrationException(FFLogsIntegrationErrors.NetworkFailure(FFLogsOperation.Authenticate));
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new FFLogsIntegrationException(MapTokenHttpError(response));
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                var root = document.RootElement;
                if (!root.TryGetProperty("access_token", out var accessTokenElement) ||
                    accessTokenElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(accessTokenElement.GetString()))
                {
                    throw new FFLogsIntegrationException(FFLogsIntegrationErrors.ProtocolFailure(FFLogsOperation.Authenticate));
                }

                var expiresInSeconds = 3600.0;
                if (root.TryGetProperty("expires_in", out var expiresElement))
                {
                    if (expiresElement.ValueKind != JsonValueKind.Number ||
                        !expiresElement.TryGetDouble(out expiresInSeconds) ||
                        !double.IsFinite(expiresInSeconds) ||
                        expiresInSeconds <= 0.0)
                    {
                        throw new FFLogsIntegrationException(FFLogsIntegrationErrors.ProtocolFailure(FFLogsOperation.Authenticate));
                    }
                }

                var token = new FFLogsAccessToken(accessTokenElement.GetString()!);
                return new CachedAccessToken(token, requestedAt.AddSeconds(expiresInSeconds));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (FFLogsIntegrationException)
            {
                throw;
            }
            catch (JsonException)
            {
                throw new FFLogsIntegrationException(FFLogsIntegrationErrors.ProtocolFailure(FFLogsOperation.Authenticate));
            }
        }
    }

    private static PullImportError MapTokenHttpError(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return FFLogsIntegrationErrors.AuthenticationFailed();
        }

        if ((int)response.StatusCode == 429)
        {
            return FFLogsIntegrationErrors.RateLimited(GetRetryAfter(response));
        }

        if ((int)response.StatusCode >= 500)
        {
            return FFLogsIntegrationErrors.Unavailable();
        }

        return FFLogsIntegrationErrors.ProtocolFailure(FFLogsOperation.Authenticate);
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (response.Headers.RetryAfter?.Date is { } retryAt)
        {
            var deltaFromNow = retryAt - DateTimeOffset.UtcNow;
            return deltaFromNow > TimeSpan.Zero ? deltaFromNow : TimeSpan.Zero;
        }

        return null;
    }

    private static bool IsUsable(CachedAccessToken token, DateTimeOffset now)
    {
        var safeExpiry = token.ExpiresAt - ExpirySafetyMargin;
        return now < safeExpiry;
    }

    private sealed record CachedAccessToken(
        FFLogsAccessToken Token,
        DateTimeOffset ExpiresAt);
}
