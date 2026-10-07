using System.Globalization;
using Microsoft.Extensions.Http.Logging;

namespace DuyTien.AssetManagement.Server;

public static class SafeGoogleHttpClientRegistrationExtensions
{
    public static IHttpClientBuilder AddSafeGoogleHttpClient(
        this IServiceCollection services,
        string name,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return services
            .AddHttpClient(name, client => client.Timeout = timeout)
            .RemoveAllLoggers()
            .AddLogger(
                serviceProvider => new SafeGoogleHttpClientLogger(
                    name,
                    serviceProvider.GetRequiredService<ILogger<SafeGoogleHttpClientLogger>>()),
                wrapHandlersPipeline: true);
    }
}

public sealed class SafeGoogleHttpClientLogger(
    string clientName,
    ILogger<SafeGoogleHttpClientLogger> logger) : IHttpClientLogger
{
    public object? LogRequestStart(HttpRequestMessage request) => null;

    public void LogRequestStop(
        object? context,
        HttpRequestMessage request,
        HttpResponseMessage response,
        TimeSpan elapsed)
    {
        logger.LogInformation(
            "Google HTTP client {HttpClientName} operation {HttpMethod} completed with status code {StatusCode} in {ElapsedMilliseconds:F0} ms.",
            clientName,
            request.Method.Method,
            (int)response.StatusCode,
            elapsed.TotalMilliseconds);
    }

    public void LogRequestFailed(
        object? context,
        HttpRequestMessage request,
        HttpResponseMessage? response,
        Exception exception,
        TimeSpan elapsed)
    {
        var statusCode = response is null
            ? "unavailable"
            : ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        logger.LogWarning(
            "Google HTTP client {HttpClientName} operation {HttpMethod} failed with status code {StatusCode} after {ElapsedMilliseconds:F0} ms; failure type {FailureType}.",
            clientName,
            request.Method.Method,
            statusCode,
            elapsed.TotalMilliseconds,
            exception.GetType().Name);
    }
}
