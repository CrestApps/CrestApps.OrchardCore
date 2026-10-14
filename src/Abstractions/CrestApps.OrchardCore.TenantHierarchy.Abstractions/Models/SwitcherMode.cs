namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Describes how the tenant switcher in a child tenant shows the list of other child tenants.
/// </summary>
public enum SwitcherMode
{
    /// <summary>
    /// The switcher links to a picker page that the parent serves.
    /// </summary>
    Hosted,

    /// <summary>
    /// The switcher opens a panel that frames a picker page served by the parent origin.
    /// </summary>
    Embedded,
}
