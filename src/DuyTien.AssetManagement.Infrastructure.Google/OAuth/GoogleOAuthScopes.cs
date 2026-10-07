using DuyTien.AssetManagement.Application.ExternalStorage;

namespace DuyTien.AssetManagement.Infrastructure.Google.OAuth;

public static class GoogleOAuthScopes
{
    public const string DriveFile = "https://www.googleapis.com/auth/drive.file";

    public static IReadOnlyList<string> Required { get; } = [DriveFile];

    public static void EnsureExactGrant(string? grantedScopes)
    {
        var granted = (grantedScopes ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        if (granted.Count != Required.Count ||
            !Required.All(granted.Contains))
        {
            throw new ExternalStorageAuthenticationException(
                "Google OAuth grant must contain exactly drive.file. Revoke the prior grant and bootstrap again.");
        }
    }
}
