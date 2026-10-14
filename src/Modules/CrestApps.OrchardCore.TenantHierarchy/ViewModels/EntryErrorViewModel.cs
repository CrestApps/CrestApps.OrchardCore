using CrestApps.OrchardCore.TenantHierarchy.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The model of the page shown when entering a child tenant did not work.
/// </summary>
public class EntryErrorViewModel
{
    /// <summary>
    /// Gets or sets why entering did not work.
    /// </summary>
    public EntryErrorKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the labels of the parent, or <see langword="null"/> in a child tenant.
    /// </summary>
    public HierarchyLabels Labels { get; set; }

    /// <summary>
    /// Gets or sets the address of the page to go back to.
    /// </summary>
    public string BackUrl { get; set; }

    /// <summary>
    /// Gets or sets the address that starts the sign-in again, or <see langword="null"/>.
    /// </summary>
    public string RetryUrl { get; set; }
}
