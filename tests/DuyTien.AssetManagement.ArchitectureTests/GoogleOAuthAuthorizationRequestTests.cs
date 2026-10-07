using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class GoogleOAuthAuthorizationRequestTests
{
    [Fact]
    public void ProvisioningBootstrap_RequestsOnlyDriveFileWithoutIncrementalGrant()
    {
        var bootstrapper = CreateBootstrapper();

        var query = ParseQuery(bootstrapper.BuildAuthorizationUri("state", "challenge"));

        Assert.Equal(GoogleOAuthScopes.DriveFile, query["scope"]);
        Assert.DoesNotContain("include_granted_scopes", query.Keys);
        Assert.DoesNotContain("trigger_onepick", query.Keys);
    }

    [Theory]
    [InlineData(
        GooglePickerResourceKind.Spreadsheet,
        "application/vnd.google-apps.spreadsheet",
        false)]
    [InlineData(
        GooglePickerResourceKind.SystemSpreadsheet,
        "application/vnd.google-apps.spreadsheet",
        false)]
    [InlineData(
        GooglePickerResourceKind.Folder,
        "application/vnd.google-apps.folder",
        true)]
    public void ExistingResourceFlow_UsesOnePickWithTheExpectedFilter(
        GooglePickerResourceKind resourceKind,
        string expectedMimeType,
        bool expectsFolderSelection)
    {
        var bootstrapper = CreateBootstrapper();

        var query = ParseQuery(
            bootstrapper.BuildAuthorizationUri("state", "challenge", resourceKind));

        Assert.Equal(GoogleOAuthScopes.DriveFile, query["scope"]);
        Assert.Equal("true", query["trigger_onepick"]);
        Assert.Equal("false", query["allow_multiple"]);
        Assert.Equal(expectedMimeType, query["mimetypes"]);
        Assert.Equal(expectsFolderSelection, query.ContainsKey("allow_folder_selection"));
        Assert.DoesNotContain("include_granted_scopes", query.Keys);
    }

    private static GoogleOAuthBootstrapper CreateBootstrapper() =>
        new(
            new GoogleWorkspaceOptions
            {
                OAuthClientId = "client-id",
                OAuthClientSecret = "client-secret",
                OAuthAuthorizationEndpoint = "https://example.test/auth"
            },
            new HttpClient());

    private static Dictionary<string, string> ParseQuery(Uri uri) =>
        uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Split('=', 2))
            .ToDictionary(
                item => Uri.UnescapeDataString(item[0]),
                item => Uri.UnescapeDataString(item[1]),
                StringComparer.Ordinal);
}
