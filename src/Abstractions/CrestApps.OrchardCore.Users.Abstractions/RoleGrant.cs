namespace CrestApps.OrchardCore.Users;

/// <summary>
/// A role that a source, such as a membership or a subscription, grants to a user.
/// </summary>
public sealed class RoleGrant
{
    /// <summary>
    /// Gets or sets the name of the role.
    /// </summary>
    public string RoleName { get; set; }

    /// <summary>
    /// Gets or sets the kind of thing that grants the role, such as <c>Membership</c> or <c>Subscription</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the ID of the thing that grants the role.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets when the grant was recorded, in UTC.
    /// </summary>
    public DateTime GrantedUtc { get; set; }
}
