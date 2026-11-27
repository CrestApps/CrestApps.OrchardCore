using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.ThemeDesigner.Models;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// A site resource, published or drafted, without its content.
/// </summary>
public sealed class SiteResourceSummary
{
    /// <summary>
    /// Gets or sets the resource name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the resource is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether the resource is a stylesheet or a script.
    /// </summary>
    public SiteResourceType Type { get; set; }

    /// <summary>
    /// Gets or sets the pages the resource is offered to.
    /// </summary>
    public SiteResourceTarget Target { get; set; }

    /// <summary>
    /// Gets or sets whether the resource is added to every page or only on demand.
    /// </summary>
    public SiteResourcePlacement Placement { get; set; }

    /// <summary>
    /// Gets or sets whether the resource is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets whether a published resource exists.
    /// </summary>
    public bool Published { get; set; }

    /// <summary>
    /// Gets or sets what the draft does to the resource, or <see langword="null"/> when the draft leaves it alone.
    /// </summary>
    public ThemeDesignChangeStatus? DraftStatus { get; set; }

    /// <summary>
    /// Gets or sets the length of the content the designer sees: the draft's when there is one.
    /// </summary>
    public int Length { get; set; }
}
