namespace DuyTien.AssetManagement.Application.Authentication;

public sealed record ScopeNode(string Id, string? ParentId);

public sealed class ScopeHierarchy
{
    private readonly IReadOnlyDictionary<string, ScopeNode> nodes;

    private ScopeHierarchy(AuthScopeType type, IReadOnlyDictionary<string, ScopeNode> nodes)
    {
        Type = type;
        this.nodes = nodes;
    }

    public AuthScopeType Type { get; }

    public static ScopeHierarchy Create(AuthScopeType type, IEnumerable<ScopeNode> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var nodes = new Dictionary<string, ScopeNode>(StringComparer.Ordinal);
        foreach (var node in source)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                throw new AuthValidationException($"{type} phải có ID.");
            }

            if (!nodes.TryAdd(node.Id, node))
            {
                throw new AuthValidationException($"{type} trùng ID '{node.Id}'.");
            }
        }

        foreach (var node in nodes.Values)
        {
            if (string.Equals(node.Id, node.ParentId, StringComparison.Ordinal))
            {
                throw new AuthValidationException($"{type} '{node.Id}' không thể là cha của chính nó.");
            }

            if (node.ParentId is not null && !nodes.ContainsKey(node.ParentId))
            {
                throw new AuthValidationException($"{type} '{node.Id}' tham chiếu ParentId không tồn tại.");
            }

            var visited = new HashSet<string>(StringComparer.Ordinal) { node.Id };
            var current = node;
            while (current.ParentId is not null)
            {
                if (!visited.Add(current.ParentId))
                {
                    throw new AuthValidationException($"Cây {type} có chu trình tại '{node.Id}'.");
                }

                current = nodes[current.ParentId];
            }
        }

        return new ScopeHierarchy(type, nodes);
    }

    public bool IsWithin(string ancestorId, string targetId, bool includeDescendants)
    {
        if (!nodes.TryGetValue(targetId, out var current) || !nodes.ContainsKey(ancestorId))
        {
            return false;
        }

        if (string.Equals(ancestorId, targetId, StringComparison.Ordinal))
        {
            return true;
        }

        if (!includeDescendants)
        {
            return false;
        }

        while (current.ParentId is not null)
        {
            if (string.Equals(current.ParentId, ancestorId, StringComparison.Ordinal))
            {
                return true;
            }

            current = nodes[current.ParentId];
        }

        return false;
    }
}

public sealed record ScopeHierarchySet(
    ScopeHierarchy OrgUnits,
    ScopeHierarchy Locations,
    ScopeHierarchy Categories)
{
    public ScopeHierarchy Get(AuthScopeType type) => type switch
    {
        AuthScopeType.OrgUnit => OrgUnits,
        AuthScopeType.Location => Locations,
        AuthScopeType.CategorySubtree => Categories,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}

public sealed record ScopeDimension(bool IsApplicable, string? Id);

public sealed record ResourceScope(
    ScopeDimension OrgUnit,
    ScopeDimension Location,
    ScopeDimension Category)
{
    public static ResourceScope Required(
        string? orgUnitId,
        string? locationId,
        string? categoryId) => new(
            new ScopeDimension(true, orgUnitId),
            new ScopeDimension(true, locationId),
            new ScopeDimension(true, categoryId));

    public ScopeDimension Get(AuthScopeType type) => type switch
    {
        AuthScopeType.OrgUnit => OrgUnit,
        AuthScopeType.Location => Location,
        AuthScopeType.CategorySubtree => Category,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}

public sealed class ScopeAuthorizationService
{
    public bool IsAllowed(
        AuthSystemState state,
        string userId,
        string permission,
        ResourceScope resource,
        ScopeHierarchySet hierarchies,
        DateTimeOffset now)
    {
        var user = state.Users.SingleOrDefault(item => item.UserId == userId);
        if (user?.State != AuthUserState.Active)
        {
            return false;
        }

        return state.RoleAssignments
            .Where(assignment => assignment.UserId == userId)
            .Where(assignment => AuthAuthorizationRules.IsEffective(assignment, now))
            .Where(assignment => AuthRoleCatalog.HasPermission(assignment.RoleCode, permission))
            .Any(assignment => Enum.GetValues<AuthScopeType>().All(type =>
                MatchesDimension(state, assignment.AssignmentId, type, resource.Get(type), hierarchies)));
    }

    public bool IsMoveAllowed(
        AuthSystemState state,
        string userId,
        string permission,
        ResourceScope source,
        ResourceScope destination,
        ScopeHierarchySet hierarchies,
        DateTimeOffset now)
    {
        var user = state.Users.SingleOrDefault(item => item.UserId == userId);
        if (user?.State != AuthUserState.Active)
        {
            return false;
        }

        return state.RoleAssignments
            .Where(assignment => assignment.UserId == userId)
            .Where(assignment => AuthAuthorizationRules.IsEffective(assignment, now))
            .Where(assignment => AuthRoleCatalog.HasPermission(assignment.RoleCode, permission))
            .Any(assignment => Enum.GetValues<AuthScopeType>().All(type =>
                MatchesDimension(state, assignment.AssignmentId, type, source.Get(type), hierarchies) &&
                MatchesDimension(state, assignment.AssignmentId, type, destination.Get(type), hierarchies)));
    }

    private static bool MatchesDimension(
        AuthSystemState state,
        string assignmentId,
        AuthScopeType type,
        ScopeDimension dimension,
        ScopeHierarchySet hierarchies)
    {
        if (!dimension.IsApplicable)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(dimension.Id))
        {
            return false;
        }

        var grants = state.RoleAssignmentScopes.Where(
            scope => scope.AssignmentId == assignmentId && scope.ScopeType == type);
        return grants.Any(grant =>
            string.Equals(grant.ScopeId, AuthScopeGrant.All, StringComparison.Ordinal) ||
            hierarchies.Get(type).IsWithin(grant.ScopeId, dimension.Id, grant.IncludeDescendants));
    }
}

public static class AuthAuthorizationRules
{
    public static bool IsEffective(AuthRoleAssignment assignment, DateTimeOffset now) =>
        assignment.State == RoleAssignmentState.Active &&
        assignment.EffectiveFrom <= now &&
        (assignment.EffectiveTo is null || assignment.EffectiveTo > now);

    public static int CountActiveAdmins(AuthSystemState state, DateTimeOffset now) =>
        state.Users.Count(user => IsActiveAdmin(state, user, now));

    public static bool IsActiveAdmin(
        AuthSystemState state,
        AuthUser user,
        DateTimeOffset now)
    {
        if (user.State != AuthUserState.Active)
        {
            return false;
        }

        return state.RoleAssignments
            .Where(assignment => assignment.UserId == user.UserId)
            .Where(assignment => string.Equals(assignment.RoleCode, AuthRole.Admin, StringComparison.Ordinal))
            .Where(assignment => IsEffective(assignment, now))
            .Any(assignment => Enum.GetValues<AuthScopeType>().All(type =>
                state.RoleAssignmentScopes.Any(scope =>
                    scope.AssignmentId == assignment.AssignmentId &&
                    scope.ScopeType == type &&
                    string.Equals(scope.ScopeId, AuthScopeGrant.All, StringComparison.Ordinal))));
    }
}
