using DuyTien.AssetManagement.Infrastructure.Google.Configuration;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class GoogleWorkspaceOptionsTests
{
    [Fact]
    public void EmptyOptions_ReportMissingOAuthAndResourceSettings()
    {
        var options = new GoogleWorkspaceOptions();

        var sheets = options.GetMissingSheetsSettings();
        var systemSheets = options.GetMissingSystemSheetsSettings();
        var drive = options.GetMissingDriveSettings();

        Assert.Contains("GoogleWorkspace:OAuthClientSecret", sheets);
        Assert.Contains("GoogleWorkspace:SpreadsheetId", sheets);
        Assert.Contains("GoogleWorkspace:SystemSpreadsheetId", systemSheets);
        Assert.Contains("GoogleWorkspace:OAuthRefreshToken", drive);
        Assert.Contains("GoogleWorkspace:DriveFolderId", drive);
        Assert.Contains(
            "GoogleWorkspace:AccessDeniedProbeFileId",
            options.GetMissingAccessBoundarySettings());
    }

    [Fact]
    public void OAuthBootstrap_DoesNotRequireRefreshTokenOrResourceIdsYet()
    {
        var options = new GoogleWorkspaceOptions
        {
            OAuthClientId = "configured",
            OAuthClientSecret = "configured"
        };

        Assert.Empty(options.GetMissingOAuthBootstrapSettings());
        Assert.Contains("GoogleWorkspace:OAuthRefreshToken", options.GetMissingSheetsSettings());
    }
}
