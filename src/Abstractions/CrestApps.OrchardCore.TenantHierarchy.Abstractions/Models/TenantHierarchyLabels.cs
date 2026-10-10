namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Holds the words a parent tenant shows on screen for the hierarchy, for example "Practice" and "Clients".
/// </summary>
public sealed class TenantHierarchyLabels
{
    /// <summary>
    /// Gets or sets the word for the parent tenant.
    /// </summary>
    public string Parent { get; set; }

    /// <summary>
    /// Gets or sets the word for one child tenant.
    /// </summary>
    public string Child { get; set; }

    /// <summary>
    /// Gets or sets the word for several child tenants.
    /// </summary>
    public string Children { get; set; }
}
