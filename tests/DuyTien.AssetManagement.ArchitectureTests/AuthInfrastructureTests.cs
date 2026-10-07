using System.Diagnostics;
using DuyTien.AssetManagement.Server.Authentication;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class AuthInfrastructureTests
{
    [Fact]
    public void Framework_hasher_does_not_store_plaintext_and_verifies_password()
    {
        var service = new FrameworkPasswordHashService();
        const string password = "username";

        var hash = service.Hash("USER-1", password);

        Assert.DoesNotContain(password, hash, StringComparison.Ordinal);
        Assert.True(service.Verify("USER-1", hash, password).Success);
        Assert.False(service.Verify("USER-1", hash, "different").Success);
    }

    [Fact]
    public async Task File_lease_excludes_another_manager_using_the_same_path()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"duytien-auth-test-{Guid.NewGuid():N}.lock");
        var first = new FileAuthWriterLeaseManager(new AuthRuntimeOptions
        {
            SystemWriterLockFilePath = path
        });
        var second = new FileAuthWriterLeaseManager(new AuthRuntimeOptions
        {
            SystemWriterLockFilePath = path
        });

        await using (await first.AcquireAsync("web-test"))
        {
            await Assert.ThrowsAsync<AuthWriterLeaseUnavailableException>(async () =>
            {
                await using var unused = await second.AcquireAsync("cli-test");
            });
        }

        await using var acquiredAfterRelease = await second.AcquireAsync("cli-test");
    }

    [Fact]
    public async Task File_lease_excludes_a_separate_auth_cli_process()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"duytien-auth-process-test-{Guid.NewGuid():N}.lock");
        var manager = new FileAuthWriterLeaseManager(new AuthRuntimeOptions
        {
            SystemWriterLockFilePath = path
        });
        await using var heldByWeb = await manager.AcquireAsync("web-test-process");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(typeof(FileAuthWriterLeaseManager).Assembly.Location);
        start.ArgumentList.Add("auth-system-init");
        start.Environment["Auth__SystemWriterLockFilePath"] = path;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start auth CLI test process.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await process.WaitForExitAsync(timeout.Token);
        var error = await process.StandardError.ReadToEndAsync(timeout.Token);

        Assert.Equal(1, process.ExitCode);
        Assert.Contains(nameof(AuthWriterLeaseUnavailableException), error, StringComparison.Ordinal);
        Assert.DoesNotContain(path, error, StringComparison.OrdinalIgnoreCase);
    }
}
