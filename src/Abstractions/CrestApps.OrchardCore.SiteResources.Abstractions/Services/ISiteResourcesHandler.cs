namespace CrestApps.OrchardCore.SiteResources.Services;

/// <summary>
/// Changes the site resources offered to the page being rendered.
/// </summary>
public interface ISiteResourcesHandler
{
    /// <summary>
    /// Called after the published resources offered to the page are known, before they are added to it.
    /// </summary>
    /// <param name="context">The resources offered to the page.</param>
    Task ResolvingAsync(SiteResourcesResolvingContext context);
}
