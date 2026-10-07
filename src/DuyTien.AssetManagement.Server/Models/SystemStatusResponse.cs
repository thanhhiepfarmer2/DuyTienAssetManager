namespace DuyTien.AssetManagement.Server.Models;

public sealed record SystemStatusResponse(
    string State,
    bool SingleWriterMode,
    ExternalStorageStatus Sheets,
    ExternalStorageStatus Drive);

public sealed record ExternalStorageStatus(
    bool ConfigurationPresent,
    bool LiveConnectionVerified);
