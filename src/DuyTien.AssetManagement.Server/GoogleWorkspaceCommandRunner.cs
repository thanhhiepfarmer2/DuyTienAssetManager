using System.Text;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Application.ProofOfConcept;
using DuyTien.AssetManagement.Infrastructure.Google;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;

namespace DuyTien.AssetManagement.Server;

internal static class GoogleWorkspaceCommandRunner
{
    public static async Task<bool> TryRunAsync(
        string[] args,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var command = args.FirstOrDefault();
        if (command is not (
            "google-oauth-bootstrap" or
            "google-picker-select" or
            "google-provision" or
            "google-live-check" or
            "google-live-access-boundary-check" or
            "google-poc-init" or
            "google-poc-run"))
        {
            return false;
        }

        try
        {
            switch (command)
            {
                case "google-oauth-bootstrap":
                    await BootstrapOAuthAsync(services, cancellationToken).ConfigureAwait(false);
                    break;
                case "google-picker-select":
                    await SelectExistingResourceAsync(
                        services,
                        ParseResourceKind(args.Skip(1).FirstOrDefault()),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case "google-provision":
                    await ProvisionResourceAsync(
                        services,
                        ParseResourceKind(args.Skip(1).FirstOrDefault()),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case "google-live-check":
                    await CheckLiveAsync(services, cancellationToken).ConfigureAwait(false);
                    break;
                case "google-live-access-boundary-check":
                    await CheckAccessBoundaryAsync(services, cancellationToken).ConfigureAwait(false);
                    break;
                case "google-poc-init":
                    await InitializePocAsync(services, cancellationToken).ConfigureAwait(false);
                    break;
                case "google-poc-run":
                    await RunPocAsync(
                        services,
                        args.Skip(1).FirstOrDefault(),
                        cancellationToken).ConfigureAwait(false);
                    break;
            }

            Environment.ExitCode = 0;
        }
        catch (Exception exception)
        {
            Environment.ExitCode = 1;
            Console.Error.WriteLine(
                $"TASK-004 command failed: {exception.GetType().Name}. No secret or Google response body was logged.");
        }

        return true;
    }

    private static async Task BootstrapOAuthAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"Opening Desktop OAuth consent. Local callback: {GoogleOAuthBootstrapper.RedirectUri}");
        var bootstrapper = services.GetRequiredService<GoogleOAuthBootstrapper>();
        var refreshToken = await bootstrapper.AuthorizeAsync(cancellationToken).ConfigureAwait(false);
        await UserSecretsRefreshTokenStore.SaveAsync(refreshToken, cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine("OAuth consent completed; refresh token saved to server user-secrets.");
    }

    private static async Task SelectExistingResourceAsync(
        IServiceProvider services,
        GooglePickerResourceKind resourceKind,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"Opening Google Picker for one existing {ResourceLabel(resourceKind)}. " +
            "Selecting a folder grants access to the folder itself, not existing child files.");
        var authorization = await services.GetRequiredService<GoogleOAuthBootstrapper>()
            .AuthorizeAndPickAsync(resourceKind, cancellationToken).ConfigureAwait(false);
        await UserSecretsRefreshTokenStore.SavePickerResultAsync(
            authorization,
            resourceKind,
            cancellationToken).ConfigureAwait(false);
        Console.WriteLine(
            $"Picker selection completed; narrow refresh token and {ResourceLabel(resourceKind)} id " +
            "saved to server user-secrets without printing values.");
    }

    private static async Task ProvisionResourceAsync(
        IServiceProvider services,
        GooglePickerResourceKind resourceKind,
        CancellationToken cancellationToken)
    {
        var options = services.GetRequiredService<GoogleWorkspaceOptions>();
        var alreadyConfigured = resourceKind switch
        {
            GooglePickerResourceKind.Spreadsheet => options.SpreadsheetId,
            GooglePickerResourceKind.SystemSpreadsheet => options.SystemSpreadsheetId,
            GooglePickerResourceKind.Folder => options.DriveFolderId,
            _ => throw new ArgumentOutOfRangeException(nameof(resourceKind))
        };
        if (!string.IsNullOrWhiteSpace(alreadyConfigured))
        {
            throw new InvalidOperationException(
                $"A {ResourceLabel(resourceKind)} id is already configured; provisioning will not replace it.");
        }

        var provisioner = services.GetRequiredService<GoogleWorkspaceProvisioner>();
        var resourceId = resourceKind switch
        {
            GooglePickerResourceKind.Spreadsheet => await provisioner.CreateSpreadsheetAsync(
                "DuyTien Asset Management - DEV",
                cancellationToken).ConfigureAwait(false),
            GooglePickerResourceKind.SystemSpreadsheet => await provisioner.CreateSpreadsheetAsync(
                "DuyTien Asset Management - DEV System",
                cancellationToken).ConfigureAwait(false),
            GooglePickerResourceKind.Folder => await provisioner.CreateFolderAsync(
                "DuyTien Asset Management - DEV Files",
                cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(resourceKind))
        };
        await UserSecretsRefreshTokenStore.SaveProvisionedResourceAsync(
            resourceKind,
            resourceId,
            cancellationToken).ConfigureAwait(false);
        Console.WriteLine(
            $"Provisioned one new {ResourceLabel(resourceKind)} and saved its id to user-secrets; " +
            "the id was not printed.");
    }

    private static async Task CheckLiveAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var sheets = await services.GetRequiredService<IAssetDataStore>()
            .CheckReadinessAsync(cancellationToken).ConfigureAwait(false);
        var drive = await services.GetRequiredService<IAssetFileStore>()
            .CheckReadinessAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine(
            $"Sheets configured={sheets.ConfigurationPresent}, live={sheets.LiveConnectionVerified}; " +
            $"Drive configured={drive.ConfigurationPresent}, live={drive.LiveConnectionVerified}.");
        if (!sheets.LiveConnectionVerified || !drive.LiveConnectionVerified)
        {
            throw new ExternalStorageException("Live Google readiness did not pass.");
        }
    }

    private static async Task CheckAccessBoundaryAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var options = services.GetRequiredService<GoogleWorkspaceOptions>();
        var missing = options.GetMissingAccessBoundarySettings();
        if (missing.Count > 0)
        {
            throw new ExternalStorageException(
                $"Google access-boundary configuration is incomplete: {string.Join(", ", missing)}.");
        }

        var verifier = services.GetRequiredService<GoogleDriveAccessVerifier>();
        await verifier.EnsureAccessibleAsync(
            options.SpreadsheetId!,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await verifier.EnsureAccessibleAsync(
                options.AccessDeniedProbeFileId!,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ExternalStorageAccessDeniedException exception)
            when (exception.StatusCode is 403 or 404)
        {
            Console.WriteLine(
                "Least-privilege live check passed: the granted file was accessible and the " +
                "operator-verified, ungranted control file was denied.");
            return;
        }

        throw new ExternalStorageException(
            "The ungranted control file was accessible. Revoke the broad grant/token before continuing.");
    }

    private static async Task InitializePocAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await CheckLiveAsync(services, cancellationToken).ConfigureAwait(false);
        await services.GetRequiredService<IPocAtomicDataStore>()
            .InitializeAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine("TASK-004 PoC sheet initialized or verified.");
    }

    private static async Task RunPocAsync(
        IServiceProvider services,
        string? suppliedRunId,
        CancellationToken cancellationToken)
    {
        await CheckLiveAsync(services, cancellationToken).ConfigureAwait(false);
        var runId = string.IsNullOrWhiteSpace(suppliedRunId)
            ? DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss")
            : suppliedRunId;
        if (runId.Length > 48 || runId.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("Run id may contain only ASCII letters, digits and '-' (max 48).");
        }

        var dataStore = services.GetRequiredService<IPocAtomicDataStore>();
        _ = await dataStore.GetCounterAsync("TASK004", cancellationToken).ConfigureAwait(false);
        var writer = services.GetRequiredService<PocAssetWriteService>();
        var firstCommand = new PocWriteCommand(
            $"task004-{runId}-a",
            "TASK004",
            "POC",
            "local-live-probe-a",
            "task004-google-tool");
        var secondCommand = firstCommand with
        {
            IdempotencyKey = $"task004-{runId}-b",
            Payload = "local-live-probe-b"
        };
        var results = await Task.WhenAll(
            writer.ExecuteAsync(firstCommand, cancellationToken),
            writer.ExecuteAsync(secondCommand, cancellationToken)).ConfigureAwait(false);
        if (results.Select(result => result.ObjectCode).Distinct(StringComparer.Ordinal).Count() != 2)
        {
            throw new InvalidOperationException("Concurrent PoC writes produced duplicate codes.");
        }

        var replay = await writer.ExecuteAsync(firstCommand, cancellationToken).ConfigureAwait(false);
        if (replay.Outcome != PocWriteOutcome.Reconciled ||
            replay.EntityId != results[0].EntityId)
        {
            throw new InvalidOperationException("Idempotent replay did not return the original entity.");
        }

        var currentCounter = await dataStore.GetCounterAsync("TASK004", cancellationToken)
            .ConfigureAwait(false);
        var versionConflictObserved = false;
        try
        {
            await writer.ExecuteAsync(
                secondCommand with
                {
                    IdempotencyKey = $"task004-{runId}-stale",
                    Payload = "must-not-commit",
                    ExpectedCounterVersion = currentCounter.Version - 1
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (VersionConflictException)
        {
            versionConflictObserved = true;
        }

        if (!versionConflictObserved)
        {
            throw new InvalidOperationException("The stale version check did not reject the write.");
        }

        var fileContent = Encoding.UTF8.GetBytes($"TASK-004 live probe {runId}\n");
        var file = await services.GetRequiredService<PocFileWriteService>()
            .UploadAndVerifyAsync(
                $"task004-file-{runId}",
                $"task004-{runId}.txt",
                "text/plain",
                fileContent,
                cancellationToken).ConfigureAwait(false);
        if (file.Status != PocFileStatus.Available)
        {
            throw new InvalidOperationException("Drive PoC file did not reach AVAILABLE.");
        }

        Console.WriteLine(
            "TASK-004 live PoC passed: 2 unique writes, idempotent replay, stale-version rejection, " +
            "Drive upload/download checksum and AVAILABLE state.");
    }

    private static GooglePickerResourceKind ParseResourceKind(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "spreadsheet" => GooglePickerResourceKind.Spreadsheet,
            "system-spreadsheet" => GooglePickerResourceKind.SystemSpreadsheet,
            "folder" => GooglePickerResourceKind.Folder,
            _ => throw new ArgumentException("Resource kind must be 'spreadsheet', 'system-spreadsheet' or 'folder'.")
        };

    private static string ResourceLabel(GooglePickerResourceKind resourceKind) => resourceKind switch
    {
        GooglePickerResourceKind.Spreadsheet => "business spreadsheet",
        GooglePickerResourceKind.SystemSpreadsheet => "system spreadsheet",
        GooglePickerResourceKind.Folder => "folder",
        _ => throw new ArgumentOutOfRangeException(nameof(resourceKind))
    };
}
