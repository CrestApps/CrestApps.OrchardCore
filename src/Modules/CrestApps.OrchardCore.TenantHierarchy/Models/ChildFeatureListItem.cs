namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// A row of the features list of a child tenant.
/// </summary>
public sealed class ChildFeatureListItem
{
    /// <summary>
    /// Gets or sets the feature.
    /// </summary>
    public ChildFeatureInfo Feature { get; set; }

    /// <summary>
    /// Gets or sets the registry entry identifier of the child tenant.
    /// </summary>
    public string EntryId { get; set; }
}
