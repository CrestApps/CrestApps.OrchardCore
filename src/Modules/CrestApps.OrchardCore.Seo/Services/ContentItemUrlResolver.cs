using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Seo.Services;

/// <summary>
/// Builds the absolute URL of a content item's page.
/// </summary>
internal sealed class ContentItemUrlResolver
{
    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemUrlResolver"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="linkGenerator">The link generator, which builds the URL of a content item.</param>
    public ContentItemUrlResolver(
        IContentManager contentManager,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator)
    {
        _contentManager = contentManager;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
    }

    /// <summary>
    /// Builds the absolute URL of a content item's page.
    /// </summary>
    /// <param name="contentItem">The content item.</param>
    /// <param name="context">The structured data context, which knows the site's URL.</param>
    /// <returns>The URL, or <see langword="null"/> when the content item has no page.</returns>
    public async Task<string> GetUrlAsync(ContentItem contentItem, StructuredDataContext context)
    {
        var metadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(contentItem);

        if (metadata.DisplayRouteValues is null)
        {
            return null;
        }

        var httpContext = _httpContextAccessor.HttpContext;

        var path = httpContext is null
            ? _linkGenerator.GetPathByRouteValues(routeName: null, metadata.DisplayRouteValues)
            : _linkGenerator.GetPathByRouteValues(httpContext, routeName: null, metadata.DisplayRouteValues);

        return context.ToAbsoluteUrl(path);
    }
}
