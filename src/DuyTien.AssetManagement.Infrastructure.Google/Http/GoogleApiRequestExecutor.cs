using System.Net;
using System.Net.Http.Headers;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;

namespace DuyTien.AssetManagement.Infrastructure.Google.Http;

public sealed class GoogleApiRequestExecutor(
    GoogleWorkspaceOptions options,
    IGoogleAccessTokenProvider tokenProvider,
    HttpClient httpClient,
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
    GoogleSheetsRequestBudget? sheetsBudget = null,
    IGoogleConnectivityGuard? connectivity = null)
{
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync =
        delayAsync ?? Task.Delay;

    public async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        GoogleRequestSemantics semantics,
        CancellationToken cancellationToken = default,
        bool acceptResumeIncomplete = false)
    {
        ArgumentNullException.ThrowIfNull(requestFactory);
        var maxAttempts = Math.Max(1, options.MaxRetryAttempts);
        var refreshedAfterUnauthorized = false;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (connectivity is not null) await connectivity.EnsureAllowedAsync(cancellationToken).ConfigureAwait(false);
            using var request = requestFactory();
            if (semantics != GoogleRequestSemantics.SafeRead) GoogleMetadataReadMemo.Invalidate();
            else if (GoogleMetadataReadMemo.Read(request) is { } memoized) return memoized;
            var metadataRevision = GoogleMetadataReadMemo.Revision;
            string accessToken;
            using (var tokenMetric = DuyTien.AssetManagement.Application.Diagnostics.PerformanceTrace.Measure("google-token"))
                accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("DuyTien-AssetManagement/1.0");

            HttpResponseMessage response;
            try
            {
                if (sheetsBudget is not null) await sheetsBudget.WaitAsync(request, cancellationToken).ConfigureAwait(false);
                if (connectivity is not null) await connectivity.EnsureAllowedAsync(cancellationToken).ConfigureAwait(false);
                using var metric = DuyTien.AssetManagement.Application.Diagnostics.PerformanceTrace.Measure("google-http");
                using var methodMetric = DuyTien.AssetManagement.Application.Diagnostics.PerformanceTrace.Measure(request.Method == HttpMethod.Get ? "google-read-http" : "google-write-http");
                response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (semantics != GoogleRequestSemantics.NonIdempotentWrite && attempt < maxAttempts)
                {
                    await delayAsync(GetRetryDelay(response: null, attempt), cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                throw new ExternalStorageTransientException(
                    "The Google API request timed out.",
                    commitOutcomeUnknown: semantics == GoogleRequestSemantics.NonIdempotentWrite,
                    exception);
            }
            catch (HttpRequestException exception)
            {
                if (semantics != GoogleRequestSemantics.NonIdempotentWrite && attempt < maxAttempts)
                {
                    await delayAsync(GetRetryDelay(response: null, attempt), cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                throw new ExternalStorageTransientException(
                    "The Google API request failed at the network layer.",
                    commitOutcomeUnknown: semantics == GoogleRequestSemantics.NonIdempotentWrite,
                    exception);
            }

            if (response.IsSuccessStatusCode || (acceptResumeIncomplete && request.Method == HttpMethod.Put && (int)response.StatusCode == 308))
            {
                try { await GoogleMetadataReadMemo.StoreAsync(request, response, metadataRevision, cancellationToken).ConfigureAwait(false); }
                catch { response.Dispose(); throw; }
                return response;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshedAfterUnauthorized)
            {
                response.Dispose();
                tokenProvider.Invalidate();
                refreshedAfterUnauthorized = true;
                attempt--;
                continue;
            }

            if (sheetsBudget is not null)
            {
                try { await sheetsBudget.RejectedAsync(request, response.StatusCode, cancellationToken).ConfigureAwait(false); }
                catch { response.Dispose(); throw; }
            }
            var transient = response.StatusCode == HttpStatusCode.TooManyRequests ||
                response.StatusCode == HttpStatusCode.RequestTimeout ||
                (int)response.StatusCode >= 500;
            var canRetry = transient &&
                (semantics != GoogleRequestSemantics.NonIdempotentWrite ||
                 response.StatusCode == HttpStatusCode.TooManyRequests);

            if (canRetry && attempt < maxAttempts)
            {
                var delay = GetRetryDelay(response, attempt);
                response.Dispose();
                await delayAsync(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var statusCode = response.StatusCode;
            response.Dispose();
            if (statusCode == HttpStatusCode.Unauthorized)
            {
                throw new ExternalStorageAuthenticationException(
                    "Google rejected the OAuth access token.");
            }

            if (statusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                throw new ExternalStorageAccessDeniedException(
                    $"Google did not grant this app access to the requested resource (HTTP {(int)statusCode}).",
                    (int)statusCode);
            }

            if (transient)
            {
                var outcomeUnknown = semantics == GoogleRequestSemantics.NonIdempotentWrite &&
                    statusCode != HttpStatusCode.TooManyRequests;
                throw new ExternalStorageTransientException(
                    $"Google API returned transient HTTP {(int)statusCode} after {attempt} attempt(s).",
                    outcomeUnknown);
            }

            throw new ExternalStorageException(
                $"Google API returned HTTP {(int)statusCode}; no response body was logged.");
        }

        throw new InvalidOperationException("Retry loop ended unexpectedly.");
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage? response, int attempt)
    {
        if (response?.Headers.RetryAfter?.Delta is { } retryAfter && retryAfter > TimeSpan.Zero)
        {
            return retryAfter;
        }

        var baseMilliseconds = Math.Max(1, options.InitialRetryDelayMilliseconds);
        return TimeSpan.FromMilliseconds(baseMilliseconds * Math.Pow(2, attempt - 1));
    }
}
