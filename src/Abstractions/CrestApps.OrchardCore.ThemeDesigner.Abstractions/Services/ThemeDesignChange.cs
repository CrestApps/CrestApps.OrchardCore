using CrestApps.OrchardCore.ThemeDesigner.Models;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// One change in a theme draft, as the review page and the AI list it.
/// </summary>
public sealed class ThemeDesignChange
{
    /// <summary>
    /// Gets or sets what the change is about.
    /// </summary>
    public ThemeDesignChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the template or resource name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what publishing the change does.
    /// </summary>
    public ThemeDesignChangeStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the change was last made.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets a key that identifies the change in the draft, such as <c>SiteTemplate:Layout</c>.
    /// </summary>
    public string Key => $"{Kind}:{Name}";
}
