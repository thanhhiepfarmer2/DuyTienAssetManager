namespace DuyTien.AssetManagement.Server.Authentication;

public sealed class AuthRuntimeOptions
{
    public const string SectionName = "Auth";

    public string SystemWriterLockFilePath { get; set; } = Path.Combine(
        AppContext.BaseDirectory,
        ".auth-locks",
        "duytien-asset-auth-system-writer.lock");

    public bool MaintenanceMode { get; init; }
}
