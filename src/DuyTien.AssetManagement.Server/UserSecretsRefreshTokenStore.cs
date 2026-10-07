using System.Text.Json;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;

namespace DuyTien.AssetManagement.Server;

internal static class UserSecretsRefreshTokenStore
{
    private const string UserSecretsId = "7fd8ca1d-c49a-4cad-9151-9d80f0cc12bc";
    private const string RefreshTokenKey = "GoogleWorkspace:OAuthRefreshToken";
    private const string SpreadsheetIdKey = "GoogleWorkspace:SpreadsheetId";
    private const string SystemSpreadsheetIdKey = "GoogleWorkspace:SystemSpreadsheetId";
    private const string DriveFolderIdKey = "GoogleWorkspace:DriveFolderId";

    public static async Task SaveAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token is required.", nameof(refreshToken));
        }

        await SaveValuesAsync(
            new Dictionary<string, string?> { [RefreshTokenKey] = refreshToken },
            cancellationToken).ConfigureAwait(false);
    }

    public static Task SavePickerResultAsync(
        GoogleOAuthAuthorizationResult authorization,
        GooglePickerResourceKind resourceKind,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorization.RefreshToken) ||
            string.IsNullOrWhiteSpace(authorization.PickedFileId))
        {
            throw new ArgumentException("Picker authorization and selected resource are required.");
        }

        return SaveValuesAsync(
            new Dictionary<string, string?>
            {
                [RefreshTokenKey] = authorization.RefreshToken,
                [ResourceKey(resourceKind)] = authorization.PickedFileId
            },
            cancellationToken);
    }

    public static Task SaveProvisionedResourceAsync(
        GooglePickerResourceKind resourceKind,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new ArgumentException("Provisioned resource id is required.", nameof(resourceId));
        }

        return SaveValuesAsync(
            new Dictionary<string, string?> { [ResourceKey(resourceKind)] = resourceId },
            cancellationToken);
    }

    public static async Task DeleteKeyAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var secretsPath = GetSecretsPath();
        if (!File.Exists(secretsPath))
        {
            return;
        }

        Dictionary<string, string?> values;
        await using (var input = File.OpenRead(secretsPath))
        {
            values = await JsonSerializer.DeserializeAsync<Dictionary<string, string?>>(
                input,
                cancellationToken: cancellationToken).ConfigureAwait(false) ??
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        if (!values.Remove(key))
        {
            return;
        }

        var temporaryPath = $"{secretsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                await JsonSerializer.SerializeAsync(
                    output,
                    values,
                    new JsonSerializerOptions { WriteIndented = true },
                    cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, secretsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async Task SaveValuesAsync(
        IReadOnlyDictionary<string, string?> updates,
        CancellationToken cancellationToken)
    {
        var secretsPath = GetSecretsPath();
        var directory = Path.GetDirectoryName(secretsPath) ??
            throw new InvalidOperationException("Cannot resolve the user-secrets directory.");
        Directory.CreateDirectory(directory);

        Dictionary<string, string?> values;
        if (File.Exists(secretsPath))
        {
            await using var input = File.OpenRead(secretsPath);
            values = await JsonSerializer.DeserializeAsync<Dictionary<string, string?>>(
                input,
                cancellationToken: cancellationToken).ConfigureAwait(false) ??
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var update in updates)
        {
            values[update.Key] = update.Value;
        }
        var temporaryPath = $"{secretsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                await JsonSerializer.SerializeAsync(
                    output,
                    values,
                    new JsonSerializerOptions { WriteIndented = true },
                    cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, secretsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string ResourceKey(GooglePickerResourceKind resourceKind) =>
        resourceKind switch
        {
            GooglePickerResourceKind.Spreadsheet => SpreadsheetIdKey,
            GooglePickerResourceKind.SystemSpreadsheet => SystemSpreadsheetIdKey,
            GooglePickerResourceKind.Folder => DriveFolderIdKey,
            _ => throw new ArgumentOutOfRangeException(nameof(resourceKind))
        };

    private static string GetSecretsPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft",
                "UserSecrets",
                UserSecretsId,
                "secrets.json");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".microsoft",
            "usersecrets",
            UserSecretsId,
            "secrets.json");
    }
}
