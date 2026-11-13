using CrestApps.OrchardCore.SiteResources.Models;

namespace CrestApps.OrchardCore.SiteResources.Services;

/// <summary>
/// A site resource offered to the page being rendered, and the address it is served from.
/// </summary>
public sealed class SiteResourceEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SiteResourceEntry"/> class.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <param name="url">The address the page loads it from.</param>
    public SiteResourceEntry(SiteResource resource, string url)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrEmpty(url);

        Resource = resource;
        Url = url;
    }

    /// <summary>
    /// Gets the resource.
    /// </summary>
    public SiteResource Resource { get; }

    /// <summary>
    /// Gets the address the page loads the resource from.
    /// </summary>
    public string Url { get; }
}
