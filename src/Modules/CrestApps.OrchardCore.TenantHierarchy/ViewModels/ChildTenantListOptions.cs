namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// Holds the search, filter and sort of the child tenants list.
/// </summary>
public class ChildTenantListOptions
{
    /// <summary>
    /// Gets or sets the search text. It matches the name, the address and the description.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the state filter.
    /// </summary>
    public ChildTenantStateFilter State { get; set; }

    /// <summary>
    /// Gets or sets the sort order.
    /// </summary>
    public ChildTenantSort OrderBy { get; set; }

    /// <summary>
    /// Gets or sets the bulk action to run.
    /// </summary>
    public ChildTenantBulkAction BulkAction { get; set; }
}
