using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.ViewModels;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Carts.Drivers;

/// <summary>
/// The products a grouped product shows: on its page a table with a quantity for each, added to the cart at once;
/// on its card a link to that page.
/// </summary>
public sealed class GroupedProductsDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    // The product listing builds this display type for each card and product page.
    private const string ProductActionDisplayType = "ProductAction";

    private readonly IContentManager _contentManager;
    private readonly IProductSnapshotResolver _snapshotResolver;
    private readonly IEnumerable<IProductAvailabilityProvider> _availability;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;
    private readonly CartOptions _options;

    internal readonly IStringLocalizer S;

    public GroupedProductsDisplayDriver(
        IContentManager contentManager,
        IProductSnapshotResolver snapshotResolver,
        IEnumerable<IProductAvailabilityProvider> availability,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        IOptions<CartOptions> options,
        IStringLocalizer<GroupedProductsDisplayDriver> stringLocalizer)
    {
        _contentManager = contentManager;
        _snapshotResolver = snapshotResolver;
        _availability = availability;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        _options = options.Value;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> DisplayAsync(ProductPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != ProductActionDisplayType ||
            !part.ContentItem.TryGet<ProductGroupPart>(out var group) ||
            group.ProductIds is not { Count: > 0 } ids)
        {
            return null;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var request = httpContext?.Request;
        var contentItem = part.ContentItem;
        var isProductPage = request?.RouteValues["contentItemId"] is string shown && shown == contentItem.ContentItemId;

        if (!isProductPage)
        {
            var url = await GetUrlAsync(contentItem, httpContext);

            return url is null
                ? null
                : Initialize<GroupedProductsViewModel>("GroupedProducts", model =>
                {
                    model.ContentItemId = contentItem.ContentItemId;
                    model.ProductUrl = url;
                }).Location(ProductActionDisplayType, "Actions:4");
        }

        var items = (await _contentManager.GetAsync(ids.Take(ProductGroupPart.MaxProducts), VersionOptions.Published))
            .Where(item => item.Has<ProductPart>())
            .ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);
        var rows = new List<GroupedProductRowViewModel>();

        // In the group's order, the products that can be bought online.
        foreach (var id in ids)
        {
            if (!items.TryGetValue(id, out var item))
            {
                continue;
            }

            var productPart = item.TryGet<ProductPart>(out var found) ? found : new ProductPart();

            // An external product is listed with the link to where it's sold.
            if (!string.IsNullOrWhiteSpace(productPart.ExternalUrl))
            {
                rows.Add(new GroupedProductRowViewModel
                {
                    ContentItemId = item.ContentItemId,
                    Title = item.DisplayText,
                    Url = await GetUrlAsync(item, httpContext),
                    Price = productPart.Price is { } listed && !string.IsNullOrEmpty(productPart.Currency) ? ProductPriceFormatter.Format(listed, productPart.Currency) : null,
                    ExternalUrl = productPart.ExternalUrl,
                    ExternalButtonText = string.IsNullOrWhiteSpace(productPart.ExternalButtonText) ? S["Buy now"].Value : productPart.ExternalButtonText,
                });

                continue;
            }

            var product = await _snapshotResolver.ResolveAsync(new ProductSnapshotContext(item));

            if (product is null)
            {
                continue;
            }

            var hasVariants = item.TryGet<ProductVariantsPart>(out var variants) && variants.HasVariants;
            var hasPrices = item.TryGet<ProductPricePart>(out var prices) && prices.Prices is { Count: > 1 };
            var stock = hasVariants ? null : (await _availability.GetAvailabilityAsync([new ProductStockKey(item.ContentItemId, null)])).GetValueOrDefault(new ProductStockKey(item.ContentItemId, null));

            rows.Add(new GroupedProductRowViewModel
            {
                ContentItemId = item.ContentItemId,
                Title = product.Title,
                Url = await GetUrlAsync(item, httpContext),
                Price = string.IsNullOrEmpty(product.Currency) ? null : ProductPriceFormatter.Format(product.UnitPrice, product.Currency),
                Availability = Describe(stock),
                ChooseOnItsPage = hasVariants || hasPrices || productPart.AddOns.Any(addOn => addOn.Required) || ProductVolumePricing.GetStartingQuantity(productPart) > 1,
                MaxQuantity = Math.Min(_options.MaxQuantity, productPart.MaxQuantity is > 0 and var max ? max : _options.MaxQuantity),
            });
        }

        if (rows.Count == 0)
        {
            return null;
        }

        return Initialize<GroupedProductsViewModel>("GroupedProducts", model =>
        {
            model.ContentItemId = contentItem.ContentItemId;
            model.Rows = rows;
            model.IsProductPage = true;
            model.ReturnUrl = request.PathBase + request.Path + request.QueryString;
        }).Location(ProductActionDisplayType, "Actions:4");
    }

    private async Task<string> GetUrlAsync(ContentItem item, HttpContext httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        var metadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(item);

        return metadata.DisplayRouteValues is null ? null : _linkGenerator.GetPathByRouteValues(httpContext, routeName: null, metadata.DisplayRouteValues);
    }

    private AddToCartAvailabilityViewModel Describe(ProductAvailability availability)
        => availability?.Status switch
        {
            null => null,
            ProductAvailabilityStatus.LowStock => new() { Label = S["Only {0} left", availability.Available], Tone = "warning", CanBuy = true },
            ProductAvailabilityStatus.OutOfStock => new() { Label = S["Out of stock"], Tone = "danger", CanBuy = false },
            ProductAvailabilityStatus.Backorder => new() { Label = S["Available on backorder"], Tone = "secondary", CanBuy = true },
            ProductAvailabilityStatus.Preorder => new() { Label = S["Pre-order: ships {0}", availability.ReleaseDateUtc.Value.ToString("D", System.Globalization.CultureInfo.CurrentCulture)], Tone = "info", CanBuy = true, IsPreorder = true },
            _ => new() { Label = S["In stock"], Tone = "success", CanBuy = true },
        };
}
