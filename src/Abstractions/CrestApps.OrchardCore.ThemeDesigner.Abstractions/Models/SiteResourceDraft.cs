using CrestApps.OrchardCore.SiteResources.Models;

namespace CrestApps.OrchardCore.ThemeDesigner.Models;

/// <summary>
/// A site resource changed in a theme draft.
/// </summary>
public sealed class SiteResourceDraft
{
    /// <summary>
    /// Gets or sets the name of the resource.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the resource as the draft has it, already minified and versioned; <see langword="null"/> when the
    /// draft removes it.
    /// </summary>
    public SiteResource Resource { get; set; }

    /// <summary>
    /// Gets or sets whether publishing the draft removes the published resource.
    /// </summary>
    public bool Removed { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the change was last made.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }
}
