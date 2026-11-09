namespace CrestApps.OrchardCore.Users.Core.Models;

/// <summary>
/// The role grants kept on a user's account: each grant by its source, and the roles that grants put the user in.
/// </summary>
public sealed class UserRoleGrants
{
    /// <summary>
    /// Gets or sets the grants, one per role, source and source ID.
    /// </summary>
    public List<RoleGrant> Grants { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles that a grant put the user in. Only these are taken away when the last grant ends; a role
    /// the user was in before keeps them in it.
    /// </summary>
    public List<string> AddedRoles { get; set; } = [];
}
