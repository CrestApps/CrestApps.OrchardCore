using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Seo.Models;

namespace CrestApps.OrchardCore.Products.Handlers;

/// <summary>
/// Keeps filtered listing pages out of search engines through Orchard Core's SEO module.
/// </summary>
/// <remarks>
/// Every combination of filters is its own URL, so a listing can produce far more pages than it has products.
/// When the address carries a filter or a keyword, the page asks for <c>noindex, follow</c>: crawlers still
/// follow its links to the products but do not index the combination. The page number, sort, page size and
/// layout do not count as filters. The canonical URL comes from the SEO part itself, which points every state of
/// a listing at the listing's own URL. An explicit robots value set on the listing's SEO part is overridden only
/// on filtered pages.
/// </remarks>
public sealed class ProductListingSeoHandler : ContentHandlerBase
{
    private const string NoIndex = "noindex, follow";

    private static readonly HashSet<string> _presentationKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ProductListingState.PageKey,
        ProductListingState.SortKey,
        ProductListingState.PageSizeKey,
        ProductListingState.ViewKey,
    };

    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProductListingSeoHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task GetContentItemAspectAsync(ContentItemAspectContext context)
    {
        if (!context.ContentItem.Has<ProductListingPart>() || !IsFiltered(_httpContextAccessor.HttpContext?.Request.Query))
        {
            return Task.CompletedTask;
        }

        return context.ForAsync<SeoAspect>(aspect =>
        {
            aspect.MetaRobots = NoIndex;

            return Task.CompletedTask;
        });
    }

    public static bool IsFiltered(IQueryCollection query)
    {
        if (query is null)
        {
            return false;
        }

        foreach (var (key, value) in query)
        {
            if (!_presentationKeys.Contains(key) && !string.IsNullOrWhiteSpace(value.ToString()))
            {
                return true;
            }
        }

        return false;
    }
}
