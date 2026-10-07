namespace DuyTien.AssetManagement.Infrastructure.Google.OAuth;

public enum GooglePickerResourceKind
{
    Spreadsheet,
    SystemSpreadsheet,
    Folder
}

public sealed record GoogleOAuthAuthorizationResult(
    string RefreshToken,
    string? PickedFileId);
