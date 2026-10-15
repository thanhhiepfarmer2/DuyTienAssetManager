using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Server.Authentication;
using DuyTien.AssetManagement.Server.Pages.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class LoginFailureDiagnosticsTests
{
    [Fact]
    public async Task OAuth_failure_is_503_with_correlation_without_password_body_or_exception_message()
    {
        var failure = new ExternalStorageAuthenticationException("FAKE sensitive response/URL must not be logged")
        { StatusCode = 400, FailureCode = "invalid_grant" };
        var logger = new CaptureLogger(); var store = new FailingStore(failure);
        var service = new AuthApplicationService(store, new FrameworkPasswordHashService(), new AuthPolicyOptions(), TimeProvider.System);
        var page = new LoginModel(service, logger)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext { TraceIdentifier = "FAKE-correlation" } },
            Input = new() { Username = "admin", Password = "FAKE-password-must-not-be-logged" }
        };
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.Equal(503, page.Response.StatusCode); Assert.Equal(1, store.Attempts);
        var error = Assert.Single(page.ModelState[string.Empty]!.Errors).ErrorMessage;
        Assert.Contains("FAKE-correlation", error);
        var log = Assert.Single(logger.Messages);
        Assert.Contains("invalid_grant", log); Assert.Contains("400", log); Assert.Contains("FAKE-correlation", log);
        Assert.DoesNotContain("FAKE sensitive", log); Assert.DoesNotContain(page.Input.Password, log);
        Assert.DoesNotContain("FAKE sensitive", error); Assert.DoesNotContain(page.Input.Password, error);
        Assert.Equal("admin", page.Input.Username); // Keep useful non-secret input.
    }

    [Fact]
    public async Task Cancelled_login_is_not_converted_to_service_failure()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var logger = new CaptureLogger(); var store = new FailingStore(new OperationCanceledException(cancellation.Token));
        var service = new AuthApplicationService(store, new FrameworkPasswordHashService(), new AuthPolicyOptions(), TimeProvider.System);
        var page = new LoginModel(service, logger) { PageContext = new PageContext { HttpContext = new DefaultHttpContext() }, Input = new() { Username = "FAKE", Password = "FAKE-pass" } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => page.OnPostAsync(cancellation.Token));
        Assert.Empty(logger.Messages);
    }

    [Fact]
    public void Diagnostic_never_copies_untrusted_failure_fields()
    {
        var description = AuthFailureDiagnostics.Describe(new ExternalStorageAuthenticationException("FAKE raw body")
        { FailureCode = "FAKE raw body", StatusCode = 400 }, "FAKE-correlation");
        Assert.Equal("unclassified", description.FailureCode); Assert.DoesNotContain("raw body", description.ToString());
        Assert.Equal(403, AuthFailureDiagnostics.Describe(new ExternalStorageAccessDeniedException("FAKE URL", 403), "FAKE").StatusCode);
    }

    private sealed class FailingStore(Exception failure) : IAuthSystemStore
    {
        public int Attempts;
        public Task<T> ReadAsync<T>(Func<AuthSystemState, T> reader, CancellationToken ct = default) => throw failure;
        public Task<T> MutateAsync<T>(Func<AuthSystemState, T> mutation, CancellationToken ct = default) { Attempts++; throw failure; }
        public Task<AuthSystemInitializationResult> InitializeAsync(string actor, CancellationToken ct = default) => throw new InvalidOperationException("Must never initialize");
    }
    private sealed class CaptureLogger : ILogger<LoginModel>
    {
        public readonly List<string> Messages = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        { Assert.Null(error); Messages.Add(formatter(state, null)); }
    }
}
