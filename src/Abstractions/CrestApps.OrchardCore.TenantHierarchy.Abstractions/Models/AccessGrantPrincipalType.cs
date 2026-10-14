namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Describes who an access grant applies to.
/// </summary>
public enum AccessGrantPrincipalType
{
    /// <summary>
    /// One parent user.
    /// </summary>
    User,

    /// <summary>
    /// Every parent user in a parent role.
    /// </summary>
    Role,
}
