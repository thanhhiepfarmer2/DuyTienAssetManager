using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using DuyTien.AssetManagement.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class SafeGoogleHttpClientLoggingTests
{
    [Fact]
    public async Task FactoryClient_LogsOperationStatusAndDurationWithoutSensitiveRequestOrResponseData()
    {
        const string resourceId = "resource-id-log-canary";
        const string querySecret = "query-log-canary";
        const string bearerToken = "bearer-log-canary";
        const string requestBody = "request-body-log-canary";
        const string responseBody = "response-body-log-canary";
        var logs = new ConcurrentQueue<string>();
        using var services = BuildServices(
            logs,
            new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(responseBody)
            }));
        var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleApiTest");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://example.test/drive/v3/files/{resourceId}?key={querySecret}")
        {
            Content = new StringContent(requestBody)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await client.SendAsync(request);
        var output = string.Join(Environment.NewLine, logs);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains(
            "Google HTTP client GoogleApiTest operation POST completed with status code 202",
            output);
        Assert.Contains(" ms.", output);
        Assert.DoesNotContain(resourceId, output, StringComparison.Ordinal);
        Assert.DoesNotContain(querySecret, output, StringComparison.Ordinal);
        Assert.DoesNotContain(bearerToken, output, StringComparison.Ordinal);
        Assert.DoesNotContain(requestBody, output, StringComparison.Ordinal);
        Assert.DoesNotContain(responseBody, output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FactoryClient_LogsFailureTypeWithoutUrlOrExceptionMessage()
    {
        const string resourceId = "failed-resource-log-canary";
        const string exceptionDetail = "exception-detail-log-canary";
        var logs = new ConcurrentQueue<string>();
        using var services = BuildServices(
            logs,
            new StubHandler(_ => throw new HttpRequestException(exceptionDetail)));
        var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleApiTest");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync($"https://example.test/drive/v3/files/{resourceId}"));
        var output = string.Join(Environment.NewLine, logs);

        Assert.Contains(
            "Google HTTP client GoogleApiTest operation GET failed with status code unavailable",
            output);
        Assert.Contains("failure type HttpRequestException", output);
        Assert.Contains(" ms;", output);
        Assert.DoesNotContain(resourceId, output, StringComparison.Ordinal);
        Assert.DoesNotContain(exceptionDetail, output, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildServices(
        ConcurrentQueue<string> logs,
        HttpMessageHandler primaryHandler)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(new CollectingLoggerProvider(logs));
        });
        services
            .AddSafeGoogleHttpClient("GoogleApiTest", TimeSpan.FromSeconds(5))
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);
        return services.BuildServiceProvider();
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }

    private sealed class CollectingLoggerProvider(ConcurrentQueue<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, logs);

        public void Dispose()
        {
        }
    }

    private sealed class CollectingLogger(
        string categoryName,
        ConcurrentQueue<string> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            logs.Enqueue($"{categoryName}: {formatter(state, exception)}");
    }
}
