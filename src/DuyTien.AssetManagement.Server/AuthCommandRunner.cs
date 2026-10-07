using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Server.Authentication;

namespace DuyTien.AssetManagement.Server;

internal static class AuthCommandRunner
{
    private const string BootstrapSecretKey = "Auth:Bootstrap:AdminPassword";
    private const string RecoverySecretKey = "Auth:Recovery:AdminPassword";

    public static async Task<bool> TryRunAsync(
        string[] args,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var command = args.FirstOrDefault();
        if (command is not ("auth-system-init" or "auth-bootstrap-admin" or "auth-recover-admin"))
        {
            return false;
        }

        try
        {
            await using var lease = await services.GetRequiredService<IAuthWriterLeaseManager>()
                .AcquireAsync(command, cancellationToken);
            switch (command)
            {
                case "auth-system-init":
                    var initialized = await services.GetRequiredService<IAuthSystemStore>()
                        .InitializeAsync("operator-cli", cancellationToken);
                    Console.WriteLine($"Auth system schema version {initialized.SchemaVersion} initialized or verified.");
                    break;
                case "auth-bootstrap-admin":
                    await BootstrapAsync(services, cancellationToken);
                    break;
                case "auth-recover-admin":
                    await RecoverAsync(services, cancellationToken);
                    break;
            }

            Environment.ExitCode = 0;
        }
        catch (Exception exception)
        {
            Environment.ExitCode = 1;
            Console.Error.WriteLine(
                $"TASK-005 auth command failed: {exception.GetType().Name}. No password, token, resource id or response body was logged.");
        }

        return true;
    }

    private static async Task BootstrapAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var password = configuration[BootstrapSecretKey];
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException($"Missing secret configuration: {BootstrapSecretKey}.");
        }

        var result = await services.GetRequiredService<AuthApplicationService>()
            .BootstrapAdminAsync(password, "operator-cli", cancellationToken);
        await UserSecretsRefreshTokenStore.DeleteKeyAsync(BootstrapSecretKey, cancellationToken);
        if (result.Created)
        {
            Console.WriteLine("Admin bootstrap committed; local user-secret key was removed when present. Remove or rotate external secret sources manually.");
        }
        else
        {
            Console.WriteLine("Admin already exists; bootstrap completed as a no-op, did not reset credentials or authorization, and removed the local bootstrap secret when present.");
        }
    }

    private static async Task RecoverAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (!services.GetRequiredService<AuthRuntimeOptions>().MaintenanceMode)
        {
            throw new InvalidOperationException("Auth:MaintenanceMode must be true for admin recovery.");
        }

        var configuration = services.GetRequiredService<IConfiguration>();
        var password = configuration[RecoverySecretKey];
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException($"Missing secret configuration: {RecoverySecretKey}.");
        }

        await services.GetRequiredService<AuthApplicationService>()
            .RecoverAdminAsync(password, "operator-cli", cancellationToken);
        await UserSecretsRefreshTokenStore.DeleteKeyAsync(RecoverySecretKey, cancellationToken);
        Console.WriteLine("Admin recovery committed; local user-secret key was removed when present. Disable maintenance mode and remove or rotate external secret sources.");
    }
}
