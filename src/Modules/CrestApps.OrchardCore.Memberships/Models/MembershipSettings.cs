namespace CrestApps.OrchardCore.Memberships.Models;

/// <summary>
/// Which roles a membership purchase may grant (Settings › Commerce › Memberships).
/// </summary>
public sealed class MembershipSettings
{
    /// <summary>
    /// Gets or sets the only roles a membership product may put its buyer in. When empty, any role but the excluded ones
    /// (Anonymous, Authenticated and Administrator) may be granted.
    /// </summary>
    public string[] GrantableRoles { get; set; } = [];
}
