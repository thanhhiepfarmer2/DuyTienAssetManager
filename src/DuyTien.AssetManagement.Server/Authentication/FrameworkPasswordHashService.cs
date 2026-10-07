using DuyTien.AssetManagement.Application.Authentication;
using Microsoft.AspNetCore.Identity;

namespace DuyTien.AssetManagement.Server.Authentication;

public sealed class FrameworkPasswordHashService : IPasswordHashService
{
    private readonly PasswordHasher<HashSubject> hasher = new();

    public string Hash(string userId, string password) =>
        hasher.HashPassword(new HashSubject(userId), password);

    public PasswordHashVerification Verify(string userId, string hash, string password)
    {
        var result = hasher.VerifyHashedPassword(new HashSubject(userId), hash, password);
        return new PasswordHashVerification(
            result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded,
            result == PasswordVerificationResult.SuccessRehashNeeded);
    }

    private sealed record HashSubject(string UserId);
}
