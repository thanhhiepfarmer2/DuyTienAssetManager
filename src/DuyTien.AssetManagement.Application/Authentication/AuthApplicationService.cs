using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DuyTien.AssetManagement.Application.Authentication;

public sealed partial class AuthApplicationService(
    IAuthSystemStore store,
    IPasswordHashService passwordHasher,
    AuthPolicyOptions policy,
    TimeProvider timeProvider)
{
    private readonly object rateLimitGate = new();
    private readonly Dictionary<string, List<DateTimeOffset>> loginAttempts =
        new(StringComparer.Ordinal);
    private readonly string dummyHash = passwordHasher.Hash("DUMMY", "not-a-user-password");

    public Task<BootstrapResult> BootstrapAdminAsync(
        string password,
        string actor,
        CancellationToken cancellationToken = default)
    {
        AuthPasswordPolicy.EnsureValid(password);
        var now = timeProvider.GetUtcNow();
        return store.MutateAsync(state =>
        {
            var existing = state.Users.SingleOrDefault(user =>
                string.Equals(user.NormalizedUsername, "ADMIN", StringComparison.Ordinal));
            if (existing is not null)
            {
                return new BootstrapResult(false, existing.UserId);
            }

            var userId = NewId();
            var user = new AuthUser(
                userId,
                "admin",
                "ADMIN",
                "Quản trị hệ thống",
                passwordHasher.Hash(userId, password),
                AuthUserState.Active,
                true,
                0,
                null,
                null,
                1,
                now,
                now,
                actor,
                now,
                actor,
                1);
            state.Users.Add(user);
            var assignment = NewAssignment(userId, AuthRole.Admin, actor, now);
            state.RoleAssignments.Add(assignment);
            AddAllScopes(state, assignment.AssignmentId);
            AddEvent(state, actor, userId, null, "ADMIN_BOOTSTRAP", SecurityEventOutcome.Success, null, now);
            return new BootstrapResult(true, userId);
        }, cancellationToken);
    }

    public Task<BootstrapResult> RecoverAdminAsync(
        string password,
        string actor,
        CancellationToken cancellationToken = default)
    {
        AuthPasswordPolicy.EnsureValid(password);
        var now = timeProvider.GetUtcNow();
        return store.MutateAsync(state =>
        {
            if (AuthAuthorizationRules.CountActiveAdmins(state, now) > 0)
            {
                throw new AuthConflictException("Vẫn còn admin hoạt động; recovery bị từ chối.");
            }

            var user = state.Users.SingleOrDefault(item =>
                string.Equals(item.NormalizedUsername, "ADMIN", StringComparison.Ordinal));
            var created = user is null;
            if (user is null)
            {
                var userId = NewId();
                user = new AuthUser(
                    userId, "admin", "ADMIN", "Quản trị hệ thống",
                    passwordHasher.Hash(userId, password), AuthUserState.Active, true,
                    0, null, null, 1, now, now, actor, now, actor, 1);
                state.Users.Add(user);
            }
            else
            {
                var updated = user with
                {
                    PasswordHash = passwordHasher.Hash(user.UserId, password),
                    State = AuthUserState.Active,
                    MustChangePassword = true,
                    FailedAccessCount = 0,
                    FailedWindowStartedAt = null,
                    LockoutEndAt = null,
                    SecurityVersion = user.SecurityVersion + 1,
                    PasswordChangedAt = now,
                    UpdatedAt = now,
                    UpdatedBy = actor,
                    Version = user.Version + 1
                };
                ReplaceUser(state, updated);
                user = updated;
                RevokeAllSessions(state, user.UserId, now, "ADMIN_RECOVERY");
            }

            var assignment = NewAssignment(user.UserId, AuthRole.Admin, actor, now);
            state.RoleAssignments.Add(assignment);
            AddAllScopes(state, assignment.AssignmentId);
            AddEvent(state, actor, user.UserId, null, "ADMIN_RECOVERY", SecurityEventOutcome.Success, null, now);
            return new BootstrapResult(created, user.UserId);
        }, cancellationToken);
    }

    public async Task<LoginResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var normalized = NormalizeUsername(request.Username);
        using var loginMetric = DuyTien.AssetManagement.Application.Diagnostics.PerformanceTrace.Measure("auth-login");
        var rateKey = $"{request.ClientKey}|{normalized}";
        if (!TryConsumeLoginAttempt(rateKey, now))
        {
            return new LoginResult(LoginStatus.RateLimited);
        }

        var rawAccessToken = NewToken();
        var rawRefreshToken = request.ClientType == AuthClientType.Mobile ? NewToken() : null;
        var result = await store.MutateAsync(state =>
        {
            var user = state.Users.SingleOrDefault(item =>
                string.Equals(item.NormalizedUsername, normalized, StringComparison.Ordinal));
            if (user is null)
            {
                _ = passwordHasher.Verify("DUMMY", dummyHash, request.Password);
                AddEvent(state, null, null, null, "LOGIN", SecurityEventOutcome.Failure, "INVALID_CREDENTIALS", now);
                return new LoginResult(LoginStatus.InvalidCredentials);
            }

            if (user.State != AuthUserState.Active || user.LockoutEndAt > now)
            {
                _ = passwordHasher.Verify(user.UserId, user.PasswordHash, request.Password);
                AddEvent(state, user.UserId, user.UserId, null, "LOGIN", SecurityEventOutcome.Failure, "INVALID_CREDENTIALS", now);
                return new LoginResult(LoginStatus.InvalidCredentials);
            }

            if (user.LockoutEndAt is not null && user.LockoutEndAt <= now)
            {
                user = user with
                {
                    FailedAccessCount = 0,
                    FailedWindowStartedAt = null,
                    LockoutEndAt = null,
                    UpdatedAt = now,
                    UpdatedBy = user.UserId,
                    Version = user.Version + 1
                };
                ReplaceUser(state, user);
            }

            var verification = passwordHasher.Verify(user.UserId, user.PasswordHash, request.Password);
            if (!verification.Success)
            {
                var withinWindow = user.FailedWindowStartedAt is not null &&
                    now - user.FailedWindowStartedAt <= policy.FailedAttemptWindow;
                var failedCount = withinWindow ? user.FailedAccessCount + 1 : 1;
                var firstFailure = withinWindow ? user.FailedWindowStartedAt : now;
                DateTimeOffset? lockoutEnd = failedCount >= policy.MaxFailedAttempts
                    ? now.Add(policy.LockoutDuration)
                    : null;
                ReplaceUser(state, user with
                {
                    FailedAccessCount = failedCount,
                    FailedWindowStartedAt = firstFailure,
                    LockoutEndAt = lockoutEnd,
                    UpdatedAt = now,
                    UpdatedBy = user.UserId,
                    Version = user.Version + 1
                });
                AddEvent(state, user.UserId, user.UserId, null, "LOGIN", SecurityEventOutcome.Failure, "INVALID_CREDENTIALS", now);
                return new LoginResult(LoginStatus.InvalidCredentials);
            }

            user = user with
            {
                PasswordHash = verification.NeedsRehash
                    ? passwordHasher.Hash(user.UserId, request.Password)
                    : user.PasswordHash,
                FailedAccessCount = 0,
                FailedWindowStartedAt = null,
                LockoutEndAt = null,
                UpdatedAt = now,
                UpdatedBy = user.UserId,
                Version = user.Version + 1
            };
            ReplaceUser(state, user);

            EnforceSessionLimit(state, user.UserId, now);
            var sessionId = NewId();
            var refreshFamilyId = request.ClientType == AuthClientType.Mobile ? NewId() : null;
            var effectiveRememberMe = request.ClientType == AuthClientType.Web &&
                request.RememberMe && !user.MustChangePassword;
            var accessExpiry = request.ClientType == AuthClientType.Mobile
                ? now.Add(policy.MobileAccess)
                : now.Add(effectiveRememberMe ? policy.RememberIdle : policy.WebIdle);
            var absoluteExpiry = request.ClientType == AuthClientType.Mobile
                ? now.Add(policy.MobileRefreshAbsolute)
                : now.Add(effectiveRememberMe ? policy.RememberAbsolute : policy.WebAbsolute);
            var session = new AuthSession(
                sessionId,
                user.UserId,
                request.ClientType,
                HashToken(rawAccessToken),
                refreshFamilyId,
                user.SecurityVersion,
                effectiveRememberMe,
                now,
                now,
                Min(accessExpiry, absoluteExpiry),
                absoluteExpiry,
                null,
                null,
                1);
            state.Sessions.Add(session);

            DateTimeOffset? refreshExpiry = null;
            if (rawRefreshToken is not null && refreshFamilyId is not null)
            {
                refreshExpiry = Min(now.Add(policy.MobileRefreshIdle), absoluteExpiry);
                state.RefreshTokens.Add(new RefreshTokenMarker(
                    NewId(), sessionId, refreshFamilyId, HashToken(rawRefreshToken),
                    RefreshTokenState.Active, now, refreshExpiry.Value, absoluteExpiry,
                    null, null, null, null, 1));
            }

            AddEvent(state, user.UserId, user.UserId, sessionId, "LOGIN", SecurityEventOutcome.Success, null, now);
            return new LoginResult(
                LoginStatus.Success,
                rawAccessToken,
                rawRefreshToken,
                sessionId,
                user.UserId,
                user.MustChangePassword,
                session.IdleExpiresAt,
                refreshExpiry,
                session.AbsoluteExpiresAt);
        }, cancellationToken);

        if (result.Status == LoginStatus.Success)
        {
            ClearLoginAttempts(rateKey);
        }

        return result;
    }

    public async Task<AuthenticatedSession?> ValidateAccessTokenAsync(
        string? rawToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = HashToken(rawToken);
        if (store is not IAuthSnapshotValidation { CanValidateReadOnly: true }) return await store.MutateAsync(state => Evaluate(state, true), cancellationToken);
        var needsTouch = false;
        var result = await store.ReadAsync(state => Evaluate(state, false), cancellationToken);
        return needsTouch ? await store.MutateAsync(state => Evaluate(state, true), cancellationToken) : result;
        AuthenticatedSession? Evaluate(AuthSystemState state, bool touch)
        {
            var session = state.Sessions.SingleOrDefault(item =>
                string.Equals(item.AccessTokenHash, tokenHash, StringComparison.Ordinal));
            if (session is null || session.RevokedAt is not null ||
                session.IdleExpiresAt <= now || session.AbsoluteExpiresAt <= now)
            {
                return null;
            }

            var user = state.Users.SingleOrDefault(item => item.UserId == session.UserId);
            if (user?.State != AuthUserState.Active || user.SecurityVersion != session.SecurityVersion)
            {
                return null;
            }

            if (now - session.LastSeenAt >= TimeSpan.FromMinutes(5))
            {
                if (!touch) { needsTouch = true; return null; }
                var idle = session.ClientType == AuthClientType.Mobile
                    ? session.IdleExpiresAt
                    : now.Add(session.RememberMe ? policy.RememberIdle : policy.WebIdle);
                session = session with
                {
                    LastSeenAt = now,
                    IdleExpiresAt = Min(idle, session.AbsoluteExpiresAt),
                    Version = session.Version + 1
                };
                ReplaceSession(state, session);
            }

            var roles = state.RoleAssignments
                .Where(assignment => assignment.UserId == user.UserId)
                .Where(assignment => AuthAuthorizationRules.IsEffective(assignment, now))
                .Select(assignment => assignment.RoleCode)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return new AuthenticatedSession(
                user.UserId,
                user.Username,
                user.DisplayName,
                session.SessionId,
                session.ClientType,
                user.MustChangePassword,
                roles,
                user.SecurityVersion);
        }
    }

    public Task<RefreshResult> RefreshAsync(
        string? rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Task.FromResult(new RefreshResult(RefreshStatus.Invalid));
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = HashToken(rawRefreshToken);
        var newAccessToken = NewToken();
        var newRefreshToken = NewToken();
        return store.MutateAsync(state =>
        {
            var marker = state.RefreshTokens.SingleOrDefault(item =>
                string.Equals(item.TokenHash, tokenHash, StringComparison.Ordinal));
            if (marker is null)
            {
                return new RefreshResult(RefreshStatus.Invalid);
            }

            if (marker.State == RefreshTokenState.Rotated)
            {
                RevokeRefreshFamily(state, marker.RefreshFamilyId, now, "REFRESH_REUSE");
                AddEvent(state, null, null, marker.SessionId, "REFRESH_REUSE", SecurityEventOutcome.Failure, "TOKEN_REUSE", now);
                return new RefreshResult(RefreshStatus.ReuseDetected);
            }

            if (marker.State != RefreshTokenState.Active ||
                marker.IdleExpiresAt <= now || marker.AbsoluteExpiresAt <= now)
            {
                return new RefreshResult(RefreshStatus.Invalid);
            }

            var session = state.Sessions.SingleOrDefault(item => item.SessionId == marker.SessionId);
            var user = session is null
                ? null
                : state.Users.SingleOrDefault(item => item.UserId == session.UserId);
            if (session is null || user?.State != AuthUserState.Active ||
                session.RevokedAt is not null || session.AbsoluteExpiresAt <= now ||
                session.SecurityVersion != user.SecurityVersion)
            {
                RevokeRefreshFamily(state, marker.RefreshFamilyId, now, "REFRESH_INVALID");
                return new RefreshResult(RefreshStatus.Invalid);
            }

            var nextMarkerId = NewId();
            var oldIndex = state.RefreshTokens.FindIndex(item => item.RefreshTokenId == marker.RefreshTokenId);
            state.RefreshTokens[oldIndex] = marker with
            {
                State = RefreshTokenState.Rotated,
                RotatedAt = now,
                ReplacedByRefreshTokenId = nextMarkerId,
                Version = marker.Version + 1
            };
            var accessExpiry = Min(now.Add(policy.MobileAccess), session.AbsoluteExpiresAt);
            var refreshExpiry = Min(now.Add(policy.MobileRefreshIdle), marker.AbsoluteExpiresAt);
            state.RefreshTokens.Add(new RefreshTokenMarker(
                nextMarkerId,
                marker.SessionId,
                marker.RefreshFamilyId,
                HashToken(newRefreshToken),
                RefreshTokenState.Active,
                now,
                refreshExpiry,
                marker.AbsoluteExpiresAt,
                null,
                null,
                null,
                null,
                1));
            ReplaceSession(state, session with
            {
                AccessTokenHash = HashToken(newAccessToken),
                LastSeenAt = now,
                IdleExpiresAt = accessExpiry,
                Version = session.Version + 1
            });
            AddEvent(state, user.UserId, user.UserId, session.SessionId, "REFRESH", SecurityEventOutcome.Success, null, now);
            return new RefreshResult(
                RefreshStatus.Success,
                newAccessToken,
                newRefreshToken,
                accessExpiry,
                refreshExpiry);
        }, cancellationToken);
    }

    public Task<bool> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        AuthPasswordPolicy.EnsureValid(newPassword);
        var now = timeProvider.GetUtcNow();
        return store.MutateAsync(state =>
        {
            var user = FindUser(state, userId);
            if (!passwordHasher.Verify(user.UserId, user.PasswordHash, currentPassword).Success)
            {
                AddEvent(state, userId, userId, null, "PASSWORD_CHANGE", SecurityEventOutcome.Failure, "INVALID_CURRENT_PASSWORD", now);
                return false;
            }

            ReplaceUser(state, user with
            {
                PasswordHash = passwordHasher.Hash(user.UserId, newPassword),
                MustChangePassword = false,
                SecurityVersion = user.SecurityVersion + 1,
                PasswordChangedAt = now,
                UpdatedAt = now,
                UpdatedBy = userId,
                Version = user.Version + 1
            });
            RevokeAllSessions(state, userId, now, "PASSWORD_CHANGED");
            AddEvent(state, userId, userId, null, "PASSWORD_CHANGE", SecurityEventOutcome.Success, null, now);
            return true;
        }, cancellationToken);
    }

    public Task LogoutAsync(string? rawAccessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawAccessToken))
        {
            return Task.CompletedTask;
        }

        var now = timeProvider.GetUtcNow();
        var hash = HashToken(rawAccessToken);
        return store.MutateAsync(state =>
        {
            var session = state.Sessions.SingleOrDefault(item => item.AccessTokenHash == hash);
            if (session is not null)
            {
                RevokeSession(state, session, now, "LOGOUT");
                AddEvent(state, session.UserId, session.UserId, session.SessionId, "LOGOUT", SecurityEventOutcome.Success, null, now);
            }

            return true;
        }, cancellationToken);
    }

    public Task LogoutAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        return store.MutateAsync(state =>
        {
            RevokeAllSessions(state, userId, now, "LOGOUT_ALL");
            AddEvent(state, userId, userId, null, "LOGOUT_ALL", SecurityEventOutcome.Success, null, now);
            return true;
        }, cancellationToken);
    }

    public Task<TemporaryPasswordResult> CreateUserAsync(
        string actorUserId,
        string username,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ValidateUsername(username);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new AuthValidationException("Tên hiển thị là bắt buộc.");
        }

        var temporaryPassword = NewTemporaryPassword();
        var normalized = NormalizeUsername(username);
        var now = timeProvider.GetUtcNow();
        return store.MutateAsync(state =>
        {
            if (state.Users.Any(user => user.NormalizedUsername == normalized))
            {
                throw new AuthConflictException("Username đã tồn tại.");
            }

            var userId = NewId();
            state.Users.Add(new AuthUser(
                userId,
                username.ToLowerInvariant(),
                normalized,
                displayName.Trim(),
                passwordHasher.Hash(userId, temporaryPassword),
                AuthUserState.Active,
                true,
                0,
                null,
                null,
                1,
                now,
                now,
                actorUserId,
                now,
                actorUserId,
                1));
            AddEvent(state, actorUserId, userId, null, "USER_CREATE", SecurityEventOutcome.Success, null, now);
            return new TemporaryPasswordResult(userId, temporaryPassword);
        }, cancellationToken);
    }

    public Task<TemporaryPasswordResult> ResetPasswordAsync(
        string actorUserId,
        string targetUserId,
        CancellationToken cancellationToken = default)
    {
        var temporaryPassword = NewTemporaryPassword();
        var now = timeProvider.GetUtcNow();
        return store.MutateAsync(state =>
        {
            var user = FindUser(state, targetUserId);
            ReplaceUser(state, user with
            {
                PasswordHash = passwordHasher.Hash(user.UserId, temporaryPassword),
                MustChangePassword = true,
                FailedAccessCount = 0,
                FailedWindowStartedAt = null,
                LockoutEndAt = null,
                SecurityVersion = user.SecurityVersion + 1,
                PasswordChangedAt = now,
                UpdatedAt = now,
                UpdatedBy = actorUserId,
                Version = user.Version + 1
            });
            RevokeAllSessions(state, user.UserId, now, "PASSWORD_RESET");
            AddEvent(state, actorUserId, user.UserId, null, "PASSWORD_RESET", SecurityEventOutcome.Success, null, now);
            return new TemporaryPasswordResult(user.UserId, temporaryPassword);
        }, cancellationToken);
    }

    public Task UpdateUserAsync(
        string actorUserId,
        string targetUserId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new AuthValidationException("Tên hiển thị là bắt buộc.");
        }

        var now = timeProvider.GetUtcNow();
        return store.MutateAsync(state =>
        {
            var user = FindUser(state, targetUserId);
            ReplaceUser(state, user with
            {
                DisplayName = displayName.Trim(),
                UpdatedAt = now,
                UpdatedBy = actorUserId,
                Version = user.Version + 1
            });
            AddEvent(state, actorUserId, targetUserId, null, "USER_UPDATE", SecurityEventOutcome.Success, null, now);
            return true;
        }, cancellationToken);
    }

    public Task SetUserLockedAsync(
        string actorUserId,
        string targetUserId,
        bool locked,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        return store.MutateAsync(state =>
        {
            var user = FindUser(state, targetUserId);
            var updated = user with
            {
                State = locked ? AuthUserState.Locked : AuthUserState.Active,
                FailedAccessCount = locked ? user.FailedAccessCount : 0,
                FailedWindowStartedAt = locked ? user.FailedWindowStartedAt : null,
                LockoutEndAt = null,
                SecurityVersion = user.SecurityVersion + 1,
                UpdatedAt = now,
                UpdatedBy = actorUserId,
                Version = user.Version + 1
            };
            ReplaceUser(state, updated);
            RevokeAllSessions(state, targetUserId, now, locked ? "USER_LOCKED" : "USER_UNLOCKED");
            EnsureActiveAdminRemains(state, now);
            AddEvent(state, actorUserId, targetUserId, null, locked ? "USER_LOCK" : "USER_UNLOCK", SecurityEventOutcome.Success, null, now);
            return true;
        }, cancellationToken);
    }

    public Task ReplaceRoleAssignmentsAsync(
        string actorUserId,
        string targetUserId,
        IReadOnlyList<RoleAssignmentInput> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var now = timeProvider.GetUtcNow();
        ValidateAssignments(inputs);
        return store.MutateAsync(state =>
        {
            var user = FindUser(state, targetUserId);
            var removedIds = state.RoleAssignments
                .Where(item => item.UserId == targetUserId)
                .Select(item => item.AssignmentId)
                .ToHashSet(StringComparer.Ordinal);
            state.RoleAssignments.RemoveAll(item => item.UserId == targetUserId);
            state.RoleAssignmentScopes.RemoveAll(item => removedIds.Contains(item.AssignmentId));
            foreach (var input in inputs)
            {
                var assignment = new AuthRoleAssignment(
                    NewId(), targetUserId, input.RoleCode, input.PositionId, "MANUAL",
                    RoleAssignmentState.Active, input.EffectiveFrom, input.EffectiveTo,
                    now, actorUserId, 1);
                state.RoleAssignments.Add(assignment);
                foreach (var scope in input.Scopes)
                {
                    state.RoleAssignmentScopes.Add(new AuthScopeGrant(
                        NewId(), assignment.AssignmentId, scope.ScopeType,
                        scope.ScopeId, scope.IncludeDescendants, 1));
                }
            }

            ReplaceUser(state, user with
            {
                SecurityVersion = user.SecurityVersion + 1,
                UpdatedAt = now,
                UpdatedBy = actorUserId,
                Version = user.Version + 1
            });
            RevokeAllSessions(state, targetUserId, now, "AUTHORIZATION_CHANGED");
            EnsureActiveAdminRemains(state, now);
            AddEvent(state, actorUserId, targetUserId, null, "AUTHORIZATION_REPLACE", SecurityEventOutcome.Success, null, now);
            return true;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<AuthUser>> ListUsersAsync(CancellationToken cancellationToken = default) =>
        store.ReadAsync<IReadOnlyList<AuthUser>>(
            state => state.Users.OrderBy(user => user.Username, StringComparer.Ordinal).ToArray(),
            cancellationToken);

    public Task<AuthUser?> GetUserAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        store.ReadAsync(
            state => state.Users.SingleOrDefault(user => user.UserId == userId),
            cancellationToken);

    public Task<IReadOnlyList<AuthRoleAssignment>> ListRoleAssignmentsAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        store.ReadAsync<IReadOnlyList<AuthRoleAssignment>>(
            state => state.RoleAssignments
                .Where(item => item.UserId == userId)
                .OrderBy(item => item.RoleCode, StringComparer.Ordinal)
                .ToArray(),
            cancellationToken);

    public Task<IReadOnlyList<AuthRoleAssignmentWithScopes>> ListRoleAssignmentsWithScopesAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        store.ReadAsync<IReadOnlyList<AuthRoleAssignmentWithScopes>>(
            state => state.RoleAssignments
                .Where(item => item.UserId == userId)
                .OrderBy(item => item.RoleCode, StringComparer.Ordinal)
                .Select(assignment => new AuthRoleAssignmentWithScopes(
                    assignment,
                    state.RoleAssignmentScopes
                        .Where(scope => scope.AssignmentId == assignment.AssignmentId)
                        .OrderBy(scope => scope.ScopeType)
                        .ThenBy(scope => scope.ScopeId, StringComparer.Ordinal)
                        .ToArray()))
                .ToArray(),
            cancellationToken);

    public Task<IReadOnlyList<AuthSecurityEvent>> ListSecurityEventsAsync(
        int take = 100,
        CancellationToken cancellationToken = default) =>
        store.ReadAsync<IReadOnlyList<AuthSecurityEvent>>(
            state => state.SecurityEvents
                .OrderByDescending(item => item.Timestamp)
                .Take(Math.Clamp(take, 1, 500))
                .ToArray(),
            cancellationToken);

    private bool TryConsumeLoginAttempt(string key, DateTimeOffset now)
    {
        lock (rateLimitGate)
        {
            if (!loginAttempts.TryGetValue(key, out var attempts))
            {
                attempts = [];
                loginAttempts[key] = attempts;
            }

            attempts.RemoveAll(timestamp => now - timestamp >= policy.LoginRateWindow);
            if (attempts.Count >= policy.LoginRateLimit)
            {
                return false;
            }

            attempts.Add(now);
            return true;
        }
    }

    private void ClearLoginAttempts(string key)
    {
        lock (rateLimitGate)
        {
            loginAttempts.Remove(key);
        }
    }

    private static void ValidateUsername(string username)
    {
        if (username.Length is < 3 or > 64 || !UsernamePattern().IsMatch(username))
        {
            throw new AuthValidationException("Username phải có 3-64 ký tự a-z, 0-9, dấu chấm, gạch dưới hoặc gạch ngang.");
        }

        if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthValidationException("Username admin dành cho bootstrap/recovery.");
        }
    }

    private static void ValidateAssignments(IReadOnlyList<RoleAssignmentInput> inputs)
    {
        foreach (var input in inputs)
        {
            if (!AuthRoleCatalog.RoleExists(input.RoleCode))
            {
                throw new AuthValidationException($"Role '{input.RoleCode}' không tồn tại.");
            }

            if (input.EffectiveTo is not null && input.EffectiveTo <= input.EffectiveFrom)
            {
                throw new AuthValidationException("EffectiveTo phải sau EffectiveFrom.");
            }

            if (input.Scopes.Any(scope => string.IsNullOrWhiteSpace(scope.ScopeId)))
            {
                throw new AuthValidationException("ScopeId là bắt buộc.");
            }

            if (input.Scopes.GroupBy(scope => (scope.ScopeType, scope.ScopeId)).Any(group => group.Count() > 1))
            {
                throw new AuthValidationException("Scope bị trùng trong một assignment.");
            }
        }
    }

    private static AuthUser FindUser(AuthSystemState state, string userId) =>
        state.Users.SingleOrDefault(item => item.UserId == userId) ??
        throw new AuthNotFoundException("Không tìm thấy tài khoản.");

    private static void EnsureActiveAdminRemains(AuthSystemState state, DateTimeOffset now)
    {
        if (AuthAuthorizationRules.CountActiveAdmins(state, now) == 0)
        {
            throw new LastActiveAdminException("Thao tác sẽ làm hệ thống không còn admin hoạt động.");
        }
    }

    private void EnforceSessionLimit(AuthSystemState state, string userId, DateTimeOffset now)
    {
        var active = state.Sessions
            .Where(item => item.UserId == userId && item.RevokedAt is null)
            .Where(item => item.IdleExpiresAt > now && item.AbsoluteExpiresAt > now)
            .OrderBy(item => item.CreatedAt)
            .ToList();
        while (active.Count >= policy.MaxActiveSessionsPerUser)
        {
            var oldest = active[0];
            active.RemoveAt(0);
            RevokeSession(state, oldest, now, "SESSION_LIMIT");
            AddEvent(state, userId, userId, oldest.SessionId, "SESSION_REVOKE_OLDEST", SecurityEventOutcome.Success, null, now);
        }
    }

    private static void RevokeAllSessions(
        AuthSystemState state,
        string userId,
        DateTimeOffset now,
        string reason)
    {
        foreach (var session in state.Sessions.Where(item => item.UserId == userId && item.RevokedAt is null).ToArray())
        {
            RevokeSession(state, session, now, reason);
        }
    }

    private static void RevokeSession(
        AuthSystemState state,
        AuthSession session,
        DateTimeOffset now,
        string reason)
    {
        ReplaceSession(state, session with
        {
            RevokedAt = now,
            RevokedReason = reason,
            Version = session.Version + 1
        });
        if (session.RefreshFamilyId is not null)
        {
            RevokeRefreshFamily(state, session.RefreshFamilyId, now, reason);
        }
    }

    private static void RevokeRefreshFamily(
        AuthSystemState state,
        string familyId,
        DateTimeOffset now,
        string reason)
    {
        for (var index = 0; index < state.RefreshTokens.Count; index++)
        {
            var marker = state.RefreshTokens[index];
            if (marker.RefreshFamilyId == familyId && marker.State == RefreshTokenState.Active)
            {
                state.RefreshTokens[index] = marker with
                {
                    State = RefreshTokenState.Revoked,
                    RevokedAt = now,
                    RevokedReason = reason,
                    Version = marker.Version + 1
                };
            }
        }

        for (var index = 0; index < state.Sessions.Count; index++)
        {
            var session = state.Sessions[index];
            if (session.RefreshFamilyId == familyId && session.RevokedAt is null)
            {
                state.Sessions[index] = session with
                {
                    RevokedAt = now,
                    RevokedReason = reason,
                    Version = session.Version + 1
                };
            }
        }
    }

    private static void ReplaceUser(AuthSystemState state, AuthUser user)
    {
        var index = state.Users.FindIndex(item => item.UserId == user.UserId);
        state.Users[index] = user;
    }

    private static void ReplaceSession(AuthSystemState state, AuthSession session)
    {
        var index = state.Sessions.FindIndex(item => item.SessionId == session.SessionId);
        state.Sessions[index] = session;
    }

    private static AuthRoleAssignment NewAssignment(
        string userId,
        string roleCode,
        string actor,
        DateTimeOffset now) =>
        new(NewId(), userId, roleCode, null, "MANUAL", RoleAssignmentState.Active,
            now, null, now, actor, 1);

    private static void AddAllScopes(AuthSystemState state, string assignmentId)
    {
        foreach (var scopeType in Enum.GetValues<AuthScopeType>())
        {
            state.RoleAssignmentScopes.Add(new AuthScopeGrant(
                NewId(), assignmentId, scopeType, AuthScopeGrant.All, true, 1));
        }
    }

    private static void AddEvent(
        AuthSystemState state,
        string? actor,
        string? target,
        string? sessionId,
        string action,
        SecurityEventOutcome outcome,
        string? failureCode,
        DateTimeOffset now) =>
        state.SecurityEvents.Add(new AuthSecurityEvent(
            NewId(), actor, target, sessionId, action, outcome, failureCode,
            NewId(), now));

    private static string NormalizeUsername(string? username) =>
        (username ?? string.Empty).Trim().ToUpperInvariant();

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string NewTemporaryPassword() => Base64Url(RandomNumberGenerator.GetBytes(18));

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) =>
        left <= right ? left : right;

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
