namespace DuyTien.AssetManagement.Application.ExternalStorage;

public sealed record StorageReadiness(
    string Provider,
    bool ConfigurationPresent,
    bool LiveConnectionVerified,
    IReadOnlyCollection<string> MissingSettings);
