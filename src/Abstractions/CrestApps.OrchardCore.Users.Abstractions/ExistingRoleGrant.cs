namespace CrestApps.OrchardCore.Users;

/// <summary>
/// A role that an <see cref="IRoleGrantSource"/> granted before grants were recorded by source.
/// </summary>
public sealed class ExistingRoleGrant
{
    /// <summary>
    /// Gets or sets the name of the role.
    /// </summary>
    public string RoleName { get; set; }

    /// <summary>
    /// Gets or sets the ID of the thing that grants the role, such as the membership's ID.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets whether the source put the user in the role. A role the user was in before keeps them in it when
    /// the grant ends.
    /// </summary>
    public bool RoleAddedBySource { get; set; }
}
