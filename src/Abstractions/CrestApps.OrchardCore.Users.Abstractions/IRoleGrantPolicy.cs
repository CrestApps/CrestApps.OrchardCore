namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Which roles something granted on a user's behalf, such as a membership or a subscription bought in the store, may put
/// them in. The tenant's administrator role is recognized whatever it is named (Orchard Core lets a tenant rename it).
/// </summary>
public interface IRoleGrantPolicy
{
    /// <summary>
    /// Whether a grant may give the role at all: never Anonymous or Authenticated, another system role, or the tenant's
    /// administrator role.
    /// </summary>
    /// <param name="roleName">The name of the role.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask<bool> IsGrantableAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the role holds a permission Orchard Core marks as security critical (managing users, roles or security
    /// settings, assigning roles, and the like), in general or for one role or type, or is the administrator role. A
    /// purchase gives such a role only when a store's allowlist names it.
    /// </summary>
    /// <param name="roleName">The name of the role.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask<bool> IsSecurityCriticalAsync(string roleName, CancellationToken cancellationToken = default);
}
