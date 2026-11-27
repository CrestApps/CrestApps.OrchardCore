using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.ThemeDesigner.Models;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// A site resource as it is published and as the draft has it.
/// </summary>
public sealed class SiteResourceDetails
{
    /// <summary>
    /// Gets or sets the published resource, or <see langword="null"/> when none is published.
    /// </summary>
    public SiteResource Published { get; set; }

    /// <summary>
    /// Gets or sets the resource as the draft has it, or <see langword="null"/> when the draft leaves it alone or
    /// removes it.
    /// </summary>
    public SiteResource Draft { get; set; }

    /// <summary>
    /// Gets or sets what the draft does to the resource, or <see langword="null"/> when the draft leaves it alone.
    /// </summary>
    public ThemeDesignChangeStatus? DraftStatus { get; set; }
}
