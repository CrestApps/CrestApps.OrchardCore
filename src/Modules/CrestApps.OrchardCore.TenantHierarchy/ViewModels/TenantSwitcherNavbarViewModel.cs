namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The model of the tenant switcher in the navbar of a child tenant. It carries the parent's name and address and
/// nothing about any other child tenant.
/// </summary>
public class TenantSwitcherNavbarViewModel
{
    /// <summary>
    /// Gets or sets the display name of the parent tenant.
    /// </summary>
    public string ParentName { get; set; }

    /// <summary>
    /// Gets or sets the display name of the current child tenant.
    /// </summary>
    public string ChildName { get; set; }

    /// <summary>
    /// Gets or sets the singular word the parent uses for a child tenant.
    /// </summary>
    public string ChildLabel { get; set; }

    /// <summary>
    /// Gets or sets the address that returns to the parent.
    /// </summary>
    public string BackUrl { get; set; }

    /// <summary>
    /// Gets or sets the address of the hosted picker of the parent.
    /// </summary>
    public string SwitchUrl { get; set; }

    /// <summary>
    /// Gets or sets the address of the embedded picker, or <see langword="null"/> in the hosted mode.
    /// </summary>
    public string EmbeddedPickerUrl { get; set; }

    /// <summary>
    /// Gets or sets the name of the parent user.
    /// </summary>
    public string UserName { get; set; }
}
