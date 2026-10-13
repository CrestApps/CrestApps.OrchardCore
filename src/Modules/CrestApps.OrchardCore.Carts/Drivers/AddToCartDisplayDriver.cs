using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.ViewModels;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Carts.Drivers;

/// <summary>
/// Offers <em>Add to cart</em> on every product that can be bought online at a one-time price, on its listing card
/// and its page, through the product's <c>ProductAction</c> display.
/// </summary>
public sealed class AddToCartDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    // The product listing builds this display type for each card and product page.
    private const string ProductActionDisplayType = "ProductAction";

    private readonly IProductSnapshotResolver _snapshotResolver;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
    private readonly CartOptions _options;

    public AddToCartDisplayDriver(
        IProductSnapshotResolver snapshotResolver,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IOptions<CartOptions> options)
    {
        _snapshotResolver = snapshotResolver;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _options = options.Value;
    }

    public override async Task<IDisplayResult> DisplayAsync(ProductPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != ProductActionDisplayType)
        {
            return null;
        }

        var contentItem = part.ContentItem;

        // A subscription plan is signed up for, not added to a cart.
        if (_options.ExcludedParts.Any(contentItem.Has))
        {
            return null;
        }

        // Only what can be bought online: the resolver refuses a listed-only product and one with no price.
        if (await _snapshotResolver.ResolveAsync(new ProductSnapshotContext(contentItem)) is null)
        {
            return null;
        }

        var prices = new List<ProductPrice>();

        if (contentItem.TryGet<ProductPricePart>(out var pricing) && pricing.Prices is { Count: > 0 })
        {
            var now = _clock.UtcNow;

            prices.AddRange(pricing.Prices.Where(price =>
                price.IsAvailable(now) &&
                price.Kind == PriceKind.OneTime &&
                !price.AllowCustomAmount));

            // A product whose prices all recur, or all ask for an amount, has nothing a cart can hold.
            if (prices.Count == 0)
            {
                return null;
            }
        }

        var defaultPrice = prices.FirstOrDefault(price => price.IsDefault) ?? prices.FirstOrDefault();
        var request = _httpContextAccessor.HttpContext?.Request;

        return Initialize<AddToCartViewModel>("AddToCart", model =>
        {
            model.ContentItemId = contentItem.ContentItemId;
            model.Prices = prices;
            model.DefaultPriceId = defaultPrice?.PriceId;
            model.MaxQuantity = defaultPrice is null
                ? _options.MaxQuantity
                : defaultPrice.AllowQuantity ? Math.Min(defaultPrice.MaximumQuantity ?? _options.MaxQuantity, _options.MaxQuantity) : 1;
            model.ReturnUrl = request is null ? null : request.PathBase + request.Path + request.QueryString;
        }).Location(ProductActionDisplayType, "Actions:5");
    }
}
