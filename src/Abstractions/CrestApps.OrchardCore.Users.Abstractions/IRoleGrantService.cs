namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Puts users in roles on behalf of the things that grant them, such as a membership or a subscription, and keeps a
/// record of each grant by its source. A role is taken away only when no source still grants it, and never when the
/// user was in the role before any source granted it.
/// </summary>
public interface IRoleGrantService
{
    /// <summary>
    /// Records that <paramref name="source"/> grants <paramref name="roleName"/> to a user, and puts the user in the
    /// role when they are not in it. Granting the same role from the same source twice records it once.
    /// </summary>
    /// <param name="userId">The unique ID of the user.</param>
    /// <param name="roleName">The name of the role.</param>
    /// <param name="source">The kind of thing that grants the role, such as <c>Membership</c>.</param>
    /// <param name="sourceId">The ID of the thing that grants the role, such as the membership's ID.</param>
    /// <returns><see langword="true"/> when the user is in the role afterwards.</returns>
    Task<bool> GrantAsync(string userId, string roleName, string source, string sourceId);

    /// <summary>
    /// Removes the grant of <paramref name="roleName"/> by <paramref name="source"/>, and takes the user out of the role
    /// when no other source still grants it and a grant put them in it.
    /// </summary>
    /// <param name="userId">The unique ID of the user.</param>
    /// <param name="roleName">The name of the role.</param>
    /// <param name="source">The kind of thing that granted the role.</param>
    /// <param name="sourceId">The ID of the thing that granted the role.</param>
    /// <returns><see langword="true"/> when the user was taken out of the role.</returns>
    Task<bool> RevokeAsync(string userId, string roleName, string source, string sourceId);

    /// <summary>
    /// Lists the grants recorded for a user.
    /// </summary>
    /// <param name="userId">The unique ID of the user.</param>
    Task<IReadOnlyList<RoleGrant>> ListAsync(string userId);
}
