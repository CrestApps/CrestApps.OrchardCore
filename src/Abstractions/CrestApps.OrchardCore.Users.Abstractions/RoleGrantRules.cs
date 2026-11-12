using OrchardCore;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Which roles a grant, such as a membership or a subscription bought in the store, may put a user in. Everyone is in
/// Anonymous and Authenticated already, and nothing granted on someone's behalf may make them an administrator.
/// </summary>
public static class RoleGrantRules
{
    /// <summary>
    /// The roles no grant ever gives.
    /// </summary>
    public static readonly IReadOnlyList<string> ExcludedRoles =
    [
        OrchardCoreConstants.Roles.Anonymous,
        OrchardCoreConstants.Roles.Authenticated,
        OrchardCoreConstants.Roles.Administrator,
    ];

    /// <summary>
    /// Returns whether a grant may give a role.
    /// </summary>
    /// <param name="roleName">The name of the role.</param>
    public static bool IsGrantable(string roleName)
        => !string.IsNullOrWhiteSpace(roleName) && !ExcludedRoles.Contains(roleName.Trim(), StringComparer.OrdinalIgnoreCase);
}
