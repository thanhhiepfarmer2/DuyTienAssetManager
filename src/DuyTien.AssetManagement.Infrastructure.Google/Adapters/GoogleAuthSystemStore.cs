using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Concurrency;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;

namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;

public sealed class GoogleAuthSystemStore(
    GoogleWorkspaceOptions options,
    GoogleApiRequestExecutor api,
    ISingleWriterCoordinator writerCoordinator) : IAuthSystemStore
{
    private const string SystemSchemaVersions = "SystemSchemaVersions";
    private const string Users = "Users";
    private const string Roles = "Roles";
    private const string Permissions = "Permissions";
    private const string RolePermissions = "RolePermissions";
    private const string Positions = "Positions";
    private const string PositionRoleTemplates = "PositionRoleTemplates";
    private const string Assignments = "UserRoleAssignments";
    private const string Scopes = "RoleAssignmentScopes";
    private const string Sessions = "Sessions";
    private const string RefreshTokens = "RefreshTokens";
    private const string SecurityEvents = "SecurityEvents";
    private const string AuthOperations = "AuthOperations";

    private static readonly IReadOnlyDictionary<string, string[]> Headers =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [SystemSchemaVersions] = ["SchemaVersion", "AppliedAt", "Reason", "AppliedBy"],
            [Users] = ["UserId", "Username", "NormalizedUsername", "DisplayName", "PasswordHash", "State", "MustChangePassword", "FailedAccessCount", "FailedWindowStartedAt", "LockoutEndAt", "SecurityVersion", "PasswordChangedAt", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "Version"],
            [Roles] = ["RoleId", "Code", "NameVi", "Description", "IsSystem", "State", "Version"],
            [Permissions] = ["PermissionId", "Code", "Resource", "Action", "Description", "Version"],
            [RolePermissions] = ["RoleId", "PermissionId", "Version"],
            [Positions] = ["PositionId", "Code", "Name", "State", "Version"],
            [PositionRoleTemplates] = ["PositionId", "RoleId", "Note", "Version"],
            [Assignments] = ["AssignmentId", "UserId", "RoleId", "PositionId", "Source", "State", "EffectiveFrom", "EffectiveTo", "CreatedAt", "CreatedBy", "Version"],
            [Scopes] = ["ScopeGrantId", "AssignmentId", "ScopeType", "ScopeId", "IncludeDescendants", "Version"],
            [Sessions] = ["SessionId", "UserId", "ClientType", "AccessTokenHash", "RefreshFamilyId", "SecurityVersion", "RememberMe", "CreatedAt", "LastSeenAt", "IdleExpiresAt", "AbsoluteExpiresAt", "RevokedAt", "RevokedReason", "Version"],
            [RefreshTokens] = ["RefreshTokenId", "SessionId", "RefreshFamilyId", "TokenHash", "State", "CreatedAt", "IdleExpiresAt", "AbsoluteExpiresAt", "RotatedAt", "ReplacedByRefreshTokenId", "RevokedAt", "RevokedReason", "Version"],
            [SecurityEvents] = ["EventId", "ActorUserId", "TargetUserId", "SessionId", "Action", "Outcome", "FailureCode", "CorrelationId", "Timestamp"],
            [AuthOperations] = ["OperationId", "IdempotencyKey", "ActorUserId", "RequestHash", "Type", "State", "ResultDigest", "CreatedAt", "CompletedAt"]
        };

    public async Task<T> ReadAsync<T>(
        Func<AuthSystemState, T> reader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader(await LoadStateAsync(cancellationToken).ConfigureAwait(false));
    }

    public Task<T> MutateAsync<T>(
        Func<AuthSystemState, T> mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        return writerCoordinator.ExecuteAsync(async token =>
        {
            var state = await LoadStateAsync(token).ConfigureAwait(false);
            var candidate = state.Clone();
            var result = mutation(candidate);
            await PersistStateAsync(candidate, token).ConfigureAwait(false);
            return result;
        }, cancellationToken);
    }

    public Task<AuthSystemInitializationResult> InitializeAsync(
        string actor,
        CancellationToken cancellationToken = default) =>
        writerCoordinator.ExecuteAsync(async token =>
        {
            EnsureConfigured();
            var spreadsheet = await GetSpreadsheetAsync(token).ConfigureAwait(false);
            var existing = spreadsheet.Sheets
                .Select(item => item.Properties.Title)
                .ToHashSet(StringComparer.Ordinal);
            var missing = Headers.Keys.Where(title => !existing.Contains(title)).ToArray();
            if (missing.Length > 0)
            {
                var addBody = new
                {
                    requests = missing.Select(title => (object)new
                    {
                        addSheet = new { properties = new { title } }
                    }).ToArray()
                };
                using var addResponse = await SendBatchAsync(
                    addBody,
                    GoogleRequestSemantics.NonIdempotentWrite,
                    token).ConfigureAwait(false);
                spreadsheet = await GetSpreadsheetAsync(token).ConfigureAwait(false);
            }

            var sheetIds = SheetIds(spreadsheet);
            var requests = new List<object>();
            foreach (var pair in Headers)
            {
                var rows = new List<string[]> { pair.Value };
                if (pair.Key == SystemSchemaVersions)
                {
                    rows.Add([AuthSystemSchema.CurrentVersion.ToString(CultureInfo.InvariantCulture), Format(DateTimeOffset.UtcNow), "TASK-005 initial schema", actor]);
                }
                else if (pair.Key == Roles)
                {
                    rows.AddRange(RoleRows());
                }
                else if (pair.Key == Permissions)
                {
                    rows.AddRange(PermissionRows());
                }
                else if (pair.Key == RolePermissions)
                {
                    rows.AddRange(RolePermissionRows());
                }

                if (pair.Key is SystemSchemaVersions or Roles or Permissions or RolePermissions)
                {
                    requests.Add(ClearAfterHeader(sheetIds[pair.Key]));
                }

                requests.Add(UpdateRows(sheetIds[pair.Key], 0, rows));
            }

            using var initializeResponse = await SendBatchAsync(
                new { requests = requests.ToArray() },
                GoogleRequestSemantics.IdempotentWrite,
                token).ConfigureAwait(false);
            return new AuthSystemInitializationResult(true, AuthSystemSchema.CurrentVersion);
        }, cancellationToken);

    private async Task<AuthSystemState> LoadStateAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var state = new AuthSystemState();
        foreach (var row in await ReadRowsAsync(Users, Headers[Users].Length, cancellationToken).ConfigureAwait(false))
        {
            state.Users.Add(new AuthUser(
                row[0], row[1], row[2], row[3], row[4], ParseEnum<AuthUserState>(row[5]),
                ParseBool(row[6]), ParseInt(row[7]), ParseNullableDate(row[8]), ParseNullableDate(row[9]),
                ParseLong(row[10]), ParseDate(row[11]), ParseDate(row[12]), row[13],
                ParseDate(row[14]), row[15], ParseInt(row[16])));
        }

        foreach (var row in await ReadRowsAsync(Assignments, Headers[Assignments].Length, cancellationToken).ConfigureAwait(false))
        {
            state.RoleAssignments.Add(new AuthRoleAssignment(
                row[0], row[1], row[2], EmptyToNull(row[3]), row[4],
                ParseEnum<RoleAssignmentState>(row[5]), ParseDate(row[6]), ParseNullableDate(row[7]),
                ParseDate(row[8]), row[9], ParseInt(row[10])));
        }

        foreach (var row in await ReadRowsAsync(Scopes, Headers[Scopes].Length, cancellationToken).ConfigureAwait(false))
        {
            state.RoleAssignmentScopes.Add(new AuthScopeGrant(
                row[0], row[1], ParseEnum<AuthScopeType>(row[2]), row[3],
                ParseBool(row[4]), ParseInt(row[5])));
        }

        foreach (var row in await ReadRowsAsync(Sessions, Headers[Sessions].Length, cancellationToken).ConfigureAwait(false))
        {
            state.Sessions.Add(new AuthSession(
                row[0], row[1], ParseEnum<AuthClientType>(row[2]), row[3], EmptyToNull(row[4]),
                ParseLong(row[5]), ParseBool(row[6]), ParseDate(row[7]), ParseDate(row[8]),
                ParseDate(row[9]), ParseDate(row[10]), ParseNullableDate(row[11]),
                EmptyToNull(row[12]), ParseInt(row[13])));
        }

        foreach (var row in await ReadRowsAsync(RefreshTokens, Headers[RefreshTokens].Length, cancellationToken).ConfigureAwait(false))
        {
            state.RefreshTokens.Add(new RefreshTokenMarker(
                row[0], row[1], row[2], row[3], ParseEnum<RefreshTokenState>(row[4]),
                ParseDate(row[5]), ParseDate(row[6]), ParseDate(row[7]), ParseNullableDate(row[8]),
                EmptyToNull(row[9]), ParseNullableDate(row[10]), EmptyToNull(row[11]), ParseInt(row[12])));
        }

        foreach (var row in await ReadRowsAsync(SecurityEvents, Headers[SecurityEvents].Length, cancellationToken).ConfigureAwait(false))
        {
            state.SecurityEvents.Add(new AuthSecurityEvent(
                row[0], EmptyToNull(row[1]), EmptyToNull(row[2]), EmptyToNull(row[3]), row[4],
                ParseEnum<SecurityEventOutcome>(row[5]), EmptyToNull(row[6]), row[7], ParseDate(row[8])));
        }

        return state;
    }

    private async Task PersistStateAsync(AuthSystemState state, CancellationToken cancellationToken)
    {
        var sheetIds = SheetIds(await GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false));
        var tables = new Dictionary<string, IEnumerable<string[]>>(StringComparer.Ordinal)
        {
            [Users] = state.Users.Select(ToRow),
            [Assignments] = state.RoleAssignments.Select(ToRow),
            [Scopes] = state.RoleAssignmentScopes.Select(ToRow),
            [Sessions] = state.Sessions.Select(ToRow),
            [RefreshTokens] = state.RefreshTokens.Select(ToRow),
            [SecurityEvents] = state.SecurityEvents.Select(ToRow)
        };
        var requests = new List<object>();
        foreach (var table in tables)
        {
            requests.Add(ClearAfterHeader(sheetIds[table.Key]));
            var rows = table.Value.ToArray();
            if (rows.Length > 0)
            {
                requests.Add(AppendRows(sheetIds[table.Key], rows));
            }
        }

        using var response = await SendBatchAsync(
            new { requests = requests.ToArray() },
            GoogleRequestSemantics.IdempotentWrite,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<SpreadsheetResponse> GetSpreadsheetAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var relative = $"spreadsheets/{Uri.EscapeDataString(options.SystemSpreadsheetId!)}" +
            "?fields=spreadsheetId,sheets.properties(sheetId,title)";
        using var response = await api.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, BuildUri(relative)),
            GoogleRequestSemantics.SafeRead,
            cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<SpreadsheetResponse>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false) ?? throw new ExternalStorageException("Google Sheets returned empty system metadata.");
    }

    private async Task<IReadOnlyList<string[]>> ReadRowsAsync(
        string sheetTitle,
        int columnCount,
        CancellationToken cancellationToken)
    {
        var lastColumn = ColumnName(columnCount);
        var range = Uri.EscapeDataString($"'{sheetTitle}'!A:{lastColumn}");
        var relative = $"spreadsheets/{Uri.EscapeDataString(options.SystemSpreadsheetId!)}/values/{range}" +
            "?majorDimension=ROWS&valueRenderOption=UNFORMATTED_VALUE";
        using var response = await api.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, BuildUri(relative)),
            GoogleRequestSemantics.SafeRead,
            cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<ValueRangeResponse>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return (result?.Values ?? [])
            .Skip(1)
            .Where(row => row.Length > 0 && !string.IsNullOrWhiteSpace(ToText(row[0])))
            .Select(row => Normalize(row, columnCount))
            .ToArray();
    }

    private Task<HttpResponseMessage> SendBatchAsync(
        object body,
        GoogleRequestSemantics semantics,
        CancellationToken cancellationToken)
    {
        var relative = $"spreadsheets/{Uri.EscapeDataString(options.SystemSpreadsheetId!)}:batchUpdate";
        return api.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, BuildUri(relative))
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            },
            semantics,
            cancellationToken);
    }

    private void EnsureConfigured()
    {
        var missing = options.GetMissingSystemSheetsSettings();
        if (missing.Count > 0)
        {
            throw new ExternalStorageException(
                $"Google system spreadsheet configuration is incomplete: {string.Join(", ", missing)}.");
        }
    }

    private Uri BuildUri(string relative) => new(new Uri(options.SheetsApiBaseUrl), relative);

    private static IReadOnlyDictionary<string, int> SheetIds(SpreadsheetResponse spreadsheet)
    {
        var values = spreadsheet.Sheets.ToDictionary(
            item => item.Properties.Title,
            item => item.Properties.SheetId,
            StringComparer.Ordinal);
        var missing = Headers.Keys.Where(title => !values.ContainsKey(title)).ToArray();
        if (missing.Length > 0)
        {
            throw new ExternalStorageException(
                $"System spreadsheet schema is missing: {string.Join(", ", missing)}. Run auth-system-init.");
        }

        return values;
    }

    private static object UpdateRows(int sheetId, int rowIndex, IEnumerable<string[]> rows) => new
    {
        updateCells = new
        {
            start = new { sheetId, rowIndex, columnIndex = 0 },
            rows = rows.Select(MakeRow).ToArray(),
            fields = "userEnteredValue"
        }
    };

    private static object ClearAfterHeader(int sheetId) => new
    {
        updateCells = new
        {
            range = new { sheetId, startRowIndex = 1 },
            rows = Array.Empty<object>(),
            fields = "userEnteredValue"
        }
    };

    private static object AppendRows(int sheetId, IEnumerable<string[]> rows) => new
    {
        appendCells = new
        {
            sheetId,
            rows = rows.Select(MakeRow).ToArray(),
            fields = "userEnteredValue"
        }
    };

    private static object MakeRow(string[] values) => new
    {
        values = values.Select(value => new
        {
            userEnteredValue = new { stringValue = value }
        }).ToArray()
    };

    private static IEnumerable<string[]> RoleRows() => AuthRole.All.Select(role =>
        new[] { role, role, RoleName(role), $"System role {role}", bool.TrueString, "ACTIVE", "1" });

    private static IEnumerable<string[]> PermissionRows() => AuthPermission.All.Select(permission =>
        new[] { permission, permission, PermissionResource(permission), PermissionAction(permission), permission, "1" });

    private static IEnumerable<string[]> RolePermissionRows() => AuthRole.All.SelectMany(role =>
        AuthRoleCatalog.GetPermissions(role).Select(permission => new[] { role, permission, "1" }));

    private static string RoleName(string role) => role switch
    {
        AuthRole.Viewer => "Người xem",
        AuthRole.AssetEditor => "Người quản lý tài sản",
        AuthRole.CatalogManager => "Người quản lý danh mục",
        AuthRole.DocumentManager => "Người quản lý tài liệu",
        AuthRole.Admin => "Quản trị hệ thống",
        _ => role
    };

    private static string PermissionResource(string permission) =>
        permission.Split('_', 2)[0];

    private static string PermissionAction(string permission) =>
        permission.Contains('_', StringComparison.Ordinal)
            ? permission[(permission.IndexOf('_') + 1)..]
            : permission;

    private static string[] ToRow(AuthUser value) =>
    [
        value.UserId, value.Username, value.NormalizedUsername, value.DisplayName,
        value.PasswordHash, value.State.ToString(), value.MustChangePassword.ToString(),
        value.FailedAccessCount.ToString(CultureInfo.InvariantCulture), Format(value.FailedWindowStartedAt),
        Format(value.LockoutEndAt), value.SecurityVersion.ToString(CultureInfo.InvariantCulture),
        Format(value.PasswordChangedAt), Format(value.CreatedAt), value.CreatedBy,
        Format(value.UpdatedAt), value.UpdatedBy, value.Version.ToString(CultureInfo.InvariantCulture)
    ];

    private static string[] ToRow(AuthRoleAssignment value) =>
    [
        value.AssignmentId, value.UserId, value.RoleCode, value.PositionId ?? string.Empty,
        value.Source, value.State.ToString(), Format(value.EffectiveFrom), Format(value.EffectiveTo),
        Format(value.CreatedAt), value.CreatedBy, value.Version.ToString(CultureInfo.InvariantCulture)
    ];

    private static string[] ToRow(AuthScopeGrant value) =>
    [
        value.ScopeGrantId, value.AssignmentId, value.ScopeType.ToString(), value.ScopeId,
        value.IncludeDescendants.ToString(), value.Version.ToString(CultureInfo.InvariantCulture)
    ];

    private static string[] ToRow(AuthSession value) =>
    [
        value.SessionId, value.UserId, value.ClientType.ToString(), value.AccessTokenHash,
        value.RefreshFamilyId ?? string.Empty, value.SecurityVersion.ToString(CultureInfo.InvariantCulture),
        value.RememberMe.ToString(), Format(value.CreatedAt), Format(value.LastSeenAt),
        Format(value.IdleExpiresAt), Format(value.AbsoluteExpiresAt), Format(value.RevokedAt),
        value.RevokedReason ?? string.Empty, value.Version.ToString(CultureInfo.InvariantCulture)
    ];

    private static string[] ToRow(RefreshTokenMarker value) =>
    [
        value.RefreshTokenId, value.SessionId, value.RefreshFamilyId, value.TokenHash,
        value.State.ToString(), Format(value.CreatedAt), Format(value.IdleExpiresAt),
        Format(value.AbsoluteExpiresAt), Format(value.RotatedAt),
        value.ReplacedByRefreshTokenId ?? string.Empty, Format(value.RevokedAt),
        value.RevokedReason ?? string.Empty, value.Version.ToString(CultureInfo.InvariantCulture)
    ];

    private static string[] ToRow(AuthSecurityEvent value) =>
    [
        value.EventId, value.ActorUserId ?? string.Empty, value.TargetUserId ?? string.Empty,
        value.SessionId ?? string.Empty, value.Action, value.Outcome.ToString(),
        value.FailureCode ?? string.Empty, value.CorrelationId, Format(value.Timestamp)
    ];

    private static string[] Normalize(JsonElement[] row, int length)
    {
        var values = Enumerable.Repeat(string.Empty, length).ToArray();
        for (var index = 0; index < Math.Min(row.Length, length); index++)
        {
            values[index] = ToText(row[index]);
        }

        return values;
    }

    private static string ToText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => bool.TrueString,
        JsonValueKind.False => bool.FalseString,
        _ => string.Empty
    };

    private static string ColumnName(int count)
    {
        var value = count;
        var result = string.Empty;
        while (value > 0)
        {
            value--;
            result = (char)('A' + value % 26) + result;
            value /= 26;
        }

        return result;
    }

    private static T ParseEnum<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ExternalStorageException($"Invalid {typeof(T).Name} in system spreadsheet.");

    private static bool ParseBool(string value) =>
        bool.TryParse(value, out var parsed)
            ? parsed
            : throw new ExternalStorageException("Invalid boolean in system spreadsheet.");

    private static int ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ExternalStorageException("Invalid integer in system spreadsheet.");

    private static long ParseLong(string value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ExternalStorageException("Invalid integer in system spreadsheet.");

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : throw new ExternalStorageException("Invalid timestamp in system spreadsheet.");

    private static DateTimeOffset? ParseNullableDate(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseDate(value);

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Format(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    private static string Format(DateTimeOffset? value) => value is null ? string.Empty : Format(value.Value);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record SpreadsheetResponse(string SpreadsheetId, SheetResponse[] Sheets);
    private sealed record SheetResponse(SheetProperties Properties);
    private sealed record SheetProperties(int SheetId, string Title);
    private sealed record ValueRangeResponse(JsonElement[][] Values);
}
