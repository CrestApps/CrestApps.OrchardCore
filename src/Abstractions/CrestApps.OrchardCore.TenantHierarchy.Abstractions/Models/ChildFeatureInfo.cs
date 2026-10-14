namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Describes one feature of a child tenant, for the features screen of the parent.
/// </summary>
public sealed class ChildFeatureInfo
{
    /// <summary>
    /// Gets or sets the feature identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the feature name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the feature category.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the feature description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the feature is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the feature is always enabled and cannot be disabled.
    /// </summary>
    public bool IsAlwaysEnabled { get; set; }

    /// <summary>
    /// Gets or sets the names of the features this feature depends on.
    /// </summary>
    public string[] Dependencies { get; set; } = [];
}
