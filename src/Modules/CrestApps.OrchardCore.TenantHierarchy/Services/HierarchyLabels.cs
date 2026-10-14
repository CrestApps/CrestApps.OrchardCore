namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Holds the words a parent tenant shows for the hierarchy, with the defaults filled in.
/// </summary>
public sealed class HierarchyLabels
{
    /// <summary>
    /// Gets or sets the word for the parent tenant, for example "Practice".
    /// </summary>
    public string Parent { get; set; }

    /// <summary>
    /// Gets or sets the word for one child tenant, for example "Client".
    /// </summary>
    public string Child { get; set; }

    /// <summary>
    /// Gets or sets the word for several child tenants, for example "Clients".
    /// </summary>
    public string Children { get; set; }

    /// <summary>
    /// Gets the word for one child tenant in lowercase, for use inside a sentence.
    /// </summary>
    public string ChildLower => Child?.ToLowerInvariant();

    /// <summary>
    /// Gets the word for several child tenants in lowercase, for use inside a sentence.
    /// </summary>
    public string ChildrenLower => Children?.ToLowerInvariant();
}
