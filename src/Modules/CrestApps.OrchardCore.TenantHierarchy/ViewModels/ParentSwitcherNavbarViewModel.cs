namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The model of the tenant picker shortcut in the navbar of a parent tenant.
/// </summary>
public class ParentSwitcherNavbarViewModel
{
    /// <summary>
    /// Gets or sets the word the parent uses for one child tenant.
    /// </summary>
    public string ChildLabel { get; set; }

    /// <summary>
    /// Gets or sets the word the parent uses for several child tenants.
    /// </summary>
    public string ChildrenLabel { get; set; }
}
