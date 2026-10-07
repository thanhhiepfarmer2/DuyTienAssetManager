namespace DuyTien.AssetManagement.Infrastructure.Google.Configuration;

public sealed class GoogleWorkspaceOptions
{
    public const string SectionName = "GoogleWorkspace";
    public string AuthorizationMode { get; init; } = "CompanyOperatorOAuth";
    public string ApplicationName { get; init; } = "DuyTien Asset Management";
    public string OAuthAuthorizationEndpoint { get; init; } =
        "https://accounts.google.com/o/oauth2/v2/auth";
    public string OAuthTokenEndpoint { get; init; } =
        "https://oauth2.googleapis.com/token";
    public string SheetsApiBaseUrl { get; init; } =
        "https://sheets.googleapis.com/v4/";
    public string DriveApiBaseUrl { get; init; } =
        "https://www.googleapis.com/";
    public int MaxRetryAttempts { get; init; } = 3;
    public int InitialRetryDelayMilliseconds { get; init; } = 200;
    public string? OAuthClientId { get; init; }
    public string? OAuthClientSecret { get; init; }
    public string? OAuthRefreshToken { get; init; }
    public string? SpreadsheetId { get; init; }
    public string? SystemSpreadsheetId { get; init; }
    public string? DriveFolderId { get; init; }
    public string? AccessDeniedProbeFileId { get; init; }

    public IReadOnlyCollection<string> GetMissingOAuthBootstrapSettings() =>
        new Dictionary<string, string?>
        {
            ["GoogleWorkspace:OAuthClientId"] = OAuthClientId,
            ["GoogleWorkspace:OAuthClientSecret"] = OAuthClientSecret
        }
        .Where(pair => string.IsNullOrWhiteSpace(pair.Value))
        .Select(pair => pair.Key)
        .ToArray();

    public IReadOnlyCollection<string> GetMissingSheetsSettings() =>
        GetMissingSettings(SpreadsheetId, "GoogleWorkspace:SpreadsheetId");

    public IReadOnlyCollection<string> GetMissingSystemSheetsSettings() =>
        GetMissingSettings(SystemSpreadsheetId, "GoogleWorkspace:SystemSpreadsheetId");

    public IReadOnlyCollection<string> GetMissingDriveSettings() =>
        GetMissingSettings(DriveFolderId, "GoogleWorkspace:DriveFolderId");

    public IReadOnlyCollection<string> GetMissingOAuthRuntimeSettings() =>
        new Dictionary<string, string?>
        {
            ["GoogleWorkspace:OAuthClientId"] = OAuthClientId,
            ["GoogleWorkspace:OAuthClientSecret"] = OAuthClientSecret,
            ["GoogleWorkspace:OAuthRefreshToken"] = OAuthRefreshToken
        }
        .Where(pair => string.IsNullOrWhiteSpace(pair.Value))
        .Select(pair => pair.Key)
        .ToArray();

    public IReadOnlyCollection<string> GetMissingAccessBoundarySettings()
    {
        var missing = GetMissingOAuthRuntimeSettings().ToList();
        if (string.IsNullOrWhiteSpace(SpreadsheetId))
        {
            missing.Add("GoogleWorkspace:SpreadsheetId");
        }

        if (string.IsNullOrWhiteSpace(AccessDeniedProbeFileId))
        {
            missing.Add("GoogleWorkspace:AccessDeniedProbeFileId");
        }

        return missing;
    }

    private IReadOnlyCollection<string> GetMissingSettings(
        string? resourceId,
        string resourceKey)
    {
        var missing = GetMissingOAuthRuntimeSettings().ToList();
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            missing.Add(resourceKey);
        }

        return missing;
    }
}
