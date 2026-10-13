using System.Text.Json;

namespace DuyTien.AssetManagement.Mobile.Client;

// No record-generated ToString for credentials. Persist only in the platform secret store.
public sealed class MobileTokens
{
    public string AccessToken { get; init; } = "";
    public string RefreshToken { get; init; } = "";
    public DateTimeOffset AccessExpiresAt { get; init; }
    public DateTimeOffset RefreshExpiresAt { get; init; }
    public bool MustChangePassword { get; init; }
    public override string ToString() => "Mobile session (redacted)";
}
public sealed class SessionEnvelope
{
    public string Backend { get; init; } = "";
    public MobileTokens Tokens { get; init; } = new();
    public bool RefreshInFlight { get; init; }
    public override string ToString() => "Stored mobile session (redacted)";
}
public sealed record MobileIdentity(string UserId, string Username, string DisplayName, string[] Roles, bool MustChangePassword);
public interface IMobileSecretStore
{
    Task<string?> ReadAsync();
    Task WriteAsync(string value);
    Task RemoveAsync();
}
public sealed class MobileApiException(int status, string message, string? fileId = null) : Exception(message)
{
    public int Status { get; } = status;
    public string? FileId { get; } = fileId;
    public bool IsUncertain => Status == 0 || Status >= 500;
}
public static class MobileEndpoint
{
    public static Uri Parse(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new MobileApiException(400, "Nhập URL backend HTTPS, không chứa credential, query hoặc fragment.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }
}
public static class MobileJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
