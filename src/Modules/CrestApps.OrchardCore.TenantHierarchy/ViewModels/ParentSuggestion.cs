namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The display name and slug suggested for a tenant that becomes a parent.
/// </summary>
public class ParentSuggestion
{
    /// <summary>
    /// Gets or sets the suggested display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the suggested slug.
    /// </summary>
    public string Slug { get; set; }
}
