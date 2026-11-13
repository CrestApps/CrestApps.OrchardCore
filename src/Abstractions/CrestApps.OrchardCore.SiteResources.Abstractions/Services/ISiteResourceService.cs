using CrestApps.OrchardCore.SiteResources.Models;

namespace CrestApps.OrchardCore.SiteResources.Services;

/// <summary>
/// Works out which site resources a page gets, and prepares a resource for serving.
/// </summary>
public interface ISiteResourceService
{
    /// <summary>
    /// Gets the enabled resources offered to the page being rendered, with the addresses they are served from, after
    /// every <see cref="ISiteResourcesHandler"/> had its say: those of the page's side (front end or admin) that are
    /// for every theme or for the theme that renders the page.
    /// </summary>
    /// <param name="isAdmin">Whether the page is an admin page.</param>
    /// <param name="themeId">The identifier of the theme that renders the page.</param>
    Task<IReadOnlyList<SiteResourceEntry>> GetPageResourcesAsync(bool isAdmin, string themeId);

    /// <summary>
    /// Gets the address a published resource is served from, with its version so browsers cache it until it changes.
    /// </summary>
    /// <param name="resource">The resource.</param>
    string GetUrl(SiteResource resource);

    /// <summary>
    /// Sets <see cref="SiteResource.ServedContent"/> and <see cref="SiteResource.Version"/> from the resource's
    /// content, minifying it when <see cref="SiteResource.Minify"/> is on.
    /// </summary>
    /// <param name="resource">The resource to prepare.</param>
    /// <returns>The minifier's errors, or an empty list. On errors the resource is served as it was written.</returns>
    IReadOnlyList<string> Prepare(SiteResource resource);
}
