namespace DuyTien.AssetManagement.Application.Authentication;

public enum AuthUserState { Active, Locked }
public enum RoleAssignmentState { Active, Revoked }
public enum AuthScopeType { OrgUnit, Location, CategorySubtree }
public enum AuthClientType { Web, Mobile }
public enum RefreshTokenState { Active, Rotated, Revoked }
public enum SecurityEventOutcome { Success, Failure }

public sealed record AuthUser(
    string UserId,
    string Username,
    string NormalizedUsername,
    string DisplayName,
    string PasswordHash,
    AuthUserState State,
    bool MustChangePassword,
    int FailedAccessCount,
    DateTimeOffset? FailedWindowStartedAt,
    DateTimeOffset? LockoutEndAt,
    long SecurityVersion,
    DateTimeOffset PasswordChangedAt,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    int Version);

public sealed record AuthRoleAssignment(
    string AssignmentId,
    string UserId,
    string RoleCode,
    string? PositionId,
    string Source,
    RoleAssignmentState State,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    int Version);

public sealed record AuthScopeGrant(
    string ScopeGrantId,
    string AssignmentId,
    AuthScopeType ScopeType,
    string ScopeId,
    bool IncludeDescendants,
    int Version)
{
    public const string All = "ALL";
}

public sealed record AuthRoleAssignmentWithScopes(
    AuthRoleAssignment Assignment,
    IReadOnlyList<AuthScopeGrant> Scopes);

public sealed record AuthSession(
    string SessionId,
    string UserId,
    AuthClientType ClientType,
    string AccessTokenHash,
    string? RefreshFamilyId,
    long SecurityVersion,
    bool RememberMe,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset IdleExpiresAt,
    DateTimeOffset AbsoluteExpiresAt,
    DateTimeOffset? RevokedAt,
    string? RevokedReason,
    int Version);

public sealed record RefreshTokenMarker(
    string RefreshTokenId,
    string SessionId,
    string RefreshFamilyId,
    string TokenHash,
    RefreshTokenState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset IdleExpiresAt,
    DateTimeOffset AbsoluteExpiresAt,
    DateTimeOffset? RotatedAt,
    string? ReplacedByRefreshTokenId,
    DateTimeOffset? RevokedAt,
    string? RevokedReason,
    int Version);

public sealed record AuthSecurityEvent(
    string EventId,
    string? ActorUserId,
    string? TargetUserId,
    string? SessionId,
    string Action,
    SecurityEventOutcome Outcome,
    string? FailureCode,
    string CorrelationId,
    DateTimeOffset Timestamp);

public sealed class AuthSystemState
{
    public List<AuthUser> Users { get; } = [];
    public List<AuthRoleAssignment> RoleAssignments { get; } = [];
    public List<AuthScopeGrant> RoleAssignmentScopes { get; } = [];
    public List<AuthSession> Sessions { get; } = [];
    public List<RefreshTokenMarker> RefreshTokens { get; } = [];
    public List<AuthSecurityEvent> SecurityEvents { get; } = [];

    public AuthSystemState Clone()
    {
        var clone = new AuthSystemState();
        clone.Users.AddRange(Users);
        clone.RoleAssignments.AddRange(RoleAssignments);
        clone.RoleAssignmentScopes.AddRange(RoleAssignmentScopes);
        clone.Sessions.AddRange(Sessions);
        clone.RefreshTokens.AddRange(RefreshTokens);
        clone.SecurityEvents.AddRange(SecurityEvents);
        return clone;
    }
}

public static class AuthSystemSchema
{
    public const int CurrentVersion = 1;
}

public sealed record AuthSystemInitializationResult(bool Changed, int SchemaVersion);

// A validated, exclusively leased local snapshot can validate without testing writer availability.
// Legacy/remote stores retain TASK-005 fail-closed mutation semantics.
public interface IAuthSnapshotValidation { bool CanValidateReadOnly { get; } }

public interface IAuthSystemStore
{
    Task<T> ReadAsync<T>(
        Func<AuthSystemState, T> reader,
        CancellationToken cancellationToken = default);

    Task<T> MutateAsync<T>(
        Func<AuthSystemState, T> mutation,
        CancellationToken cancellationToken = default);

    Task<AuthSystemInitializationResult> InitializeAsync(
        string actor,
        CancellationToken cancellationToken = default);
}

public interface IPasswordHashService
{
    string Hash(string userId, string password);
    PasswordHashVerification Verify(string userId, string hash, string password);
}

public sealed record PasswordHashVerification(bool Success, bool NeedsRehash);

public sealed class AuthValidationException(string message) : Exception(message);
public sealed class AuthNotFoundException(string message) : Exception(message);
public sealed class LastActiveAdminException(string message) : Exception(message);
public sealed class AuthConflictException(string message) : Exception(message);

public sealed record LoginRequest(
    string Username,
    string Password,
    AuthClientType ClientType,
    bool RememberMe,
    string ClientKey);

public enum LoginStatus { Success, InvalidCredentials, RateLimited }

public sealed record LoginResult(
    LoginStatus Status,
    string? AccessToken = null,
    string? RefreshToken = null,
    string? SessionId = null,
    string? UserId = null,
    bool MustChangePassword = false,
    DateTimeOffset? AccessExpiresAt = null,
    DateTimeOffset? RefreshExpiresAt = null,
    DateTimeOffset? AbsoluteExpiresAt = null);

public enum RefreshStatus { Success, Invalid, ReuseDetected }

public sealed record RefreshResult(
    RefreshStatus Status,
    string? AccessToken = null,
    string? RefreshToken = null,
    DateTimeOffset? AccessExpiresAt = null,
    DateTimeOffset? RefreshExpiresAt = null);

public sealed record AuthenticatedSession(
    string UserId,
    string Username,
    string DisplayName,
    string SessionId,
    AuthClientType ClientType,
    bool MustChangePassword,
    IReadOnlyList<string> Roles,
    long SecurityVersion);

public sealed record BootstrapResult(bool Created, string UserId);
public sealed record TemporaryPasswordResult(string UserId, string TemporaryPassword);

public sealed record ScopeGrantInput(
    AuthScopeType ScopeType,
    string ScopeId,
    bool IncludeDescendants);

public sealed record RoleAssignmentInput(
    string RoleCode,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<ScopeGrantInput> Scopes,
    string? PositionId = null);

public sealed class AuthPolicyOptions
{
    public int MaxFailedAttempts { get; init; } = 5;
    public TimeSpan FailedAttemptWindow { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);
    public int LoginRateLimit { get; init; } = 10;
    public TimeSpan LoginRateWindow { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan WebIdle { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan WebAbsolute { get; init; } = TimeSpan.FromHours(8);
    public TimeSpan RememberIdle { get; init; } = TimeSpan.FromHours(24);
    public TimeSpan RememberAbsolute { get; init; } = TimeSpan.FromDays(7);
    public TimeSpan MobileAccess { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan MobileRefreshIdle { get; init; } = TimeSpan.FromDays(14);
    public TimeSpan MobileRefreshAbsolute { get; init; } = TimeSpan.FromDays(30);
    public int MaxActiveSessionsPerUser { get; init; } = 5;
}

public static class AuthPasswordPolicy
{
    public const int MinimumLength = 8;

    public static bool IsValid(string? password) =>
        password is not null && password.Length >= MinimumLength;

    public static void EnsureValid(string? password)
    {
        if (!IsValid(password))
        {
            throw new AuthValidationException("Mật khẩu phải có ít nhất 8 ký tự.");
        }
    }
}
