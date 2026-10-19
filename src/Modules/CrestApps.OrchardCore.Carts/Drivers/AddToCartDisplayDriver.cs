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
    private readonly IEnumerable<IProductAvailabilityProvider> _availability;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
    private readonly IContentManager _contentManager;
    private readonly LinkGenerator _linkGenerator;
    private readonly CartOptions _options;

    internal readonly IStringLocalizer S;

    public AddToCartDisplayDriver(
        IProductSnapshotResolver snapshotResolver,
        IEnumerable<IProductAvailabilityProvider> availability,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IContentManager contentManager,
        LinkGenerator linkGenerator,
        IOptions<CartOptions> options,
        IStringLocalizer<AddToCartDisplayDriver> stringLocalizer)
    {
        _contentManager = contentManager;
        _linkGenerator = linkGenerator;
        _snapshotResolver = snapshotResolver;
        _availability = availability;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _options = options.Value;
        S = stringLocalizer;
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

        // A product sold in variants is bought as one of those on sale.
        List<ProductVariant> variants = contentItem.TryGet<ProductVariantsPart>(out var variantsPart) && variantsPart.HasVariants
            ? variantsPart.GetAvailable().ToList()
            : [];

        if (variantsPart?.HasVariants == true && variants.Count == 0)
        {
            return null;
        }

        // Only what can be bought online: the resolver refuses a listed-only product and one with no price.
        var product = await _snapshotResolver.ResolveAsync(new ProductSnapshotContext(contentItem) { VariantId = variants.FirstOrDefault()?.VariantId });

        if (product is null)
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

        // What's in stock of the product, or of each variant on sale, for products whose stock is counted.
        var keys = variants.Count > 0
            ? variants.Select(variant => new ProductStockKey(contentItem.ContentItemId, variant.VariantId)).ToArray()
            : [new ProductStockKey(contentItem.ContentItemId, null)];
        var stock = await _availability.GetAvailabilityAsync(keys);

        // Add-ons are chosen on the product's own page; a card links there when one is required.
        var addOns = part.AddOns?.Where(addOn => !string.IsNullOrEmpty(addOn.AddOnId) && !string.IsNullOrWhiteSpace(addOn.Label)).ToList() ?? [];
        var isProductPage = request?.RouteValues["contentItemId"] is string shown && shown == contentItem.ContentItemId;
        string productUrl = null;

        if (addOns.Count > 0 && !isProductPage && _httpContextAccessor.HttpContext is { } httpContext)
        {
            var metadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(contentItem);

            productUrl = metadata.DisplayRouteValues is null ? null : _linkGenerator.GetPathByRouteValues(httpContext, routeName: null, metadata.DisplayRouteValues);
        }

        return Initialize<AddToCartViewModel>("AddToCart", model =>
        {
            model.ContentItemId = contentItem.ContentItemId;
            model.Prices = prices;
            model.DefaultPriceId = defaultPrice?.PriceId;

            var optionNames = variantsPart?.Options?.Select(option => option.Name).ToArray();
            var productPrice = contentItem.TryGet<ProductPart>(out var productPart) ? productPart.Price : null;

            model.Variants = variants
                .Select(variant => new AddToCartVariantViewModel
                {
                    VariantId = variant.VariantId,
                    Title = variant.GetTitle(optionNames),
                    Sku = string.IsNullOrEmpty(variant.Sku) ? productPart?.Sku : variant.Sku,
                    Price = ProductSale.GetPrice(productPart, variant, _clock.UtcNow) is { } amount && !string.IsNullOrEmpty(product.Currency)
                        ? ProductPriceFormatter.Format(amount, product.Currency)
                        : null,
                    Options = new Dictionary<string, string>(variant.Options, StringComparer.OrdinalIgnoreCase),
                    ImagePath = variant.ImagePath,
                    Availability = Describe(stock.GetValueOrDefault(new ProductStockKey(contentItem.ContentItemId, variant.VariantId))),
                })
                .ToList();

            model.Availability = variants.Count == 0
                ? Describe(stock.GetValueOrDefault(new ProductStockKey(contentItem.ContentItemId, null)))
                : null;

            // One list per option, with the values the variants on sale come in, in the order the product gives them.
            model.Options = (variantsPart?.Options ?? [])
                .Where(option => !string.IsNullOrEmpty(option?.Name))
                .Select(option => new AddToCartOptionViewModel
                {
                    Name = option.Name,
                    Values = (option.Values ?? [])
                        .Where(value => variants.Any(variant => variant.Options.TryGetValue(option.Name, out var chosen) && string.Equals(chosen, value, StringComparison.OrdinalIgnoreCase)))
                        .ToList(),
                })
                .Where(option => option.Values.Count > 0)
                .ToList();
            model.MaxQuantity = defaultPrice is null
                ? _options.MaxQuantity
                : defaultPrice.AllowQuantity ? Math.Min(defaultPrice.MaximumQuantity ?? _options.MaxQuantity, _options.MaxQuantity) : 1;
            model.ReturnUrl = request is null ? null : request.PathBase + request.Path + request.QueryString;
            model.AddOns = addOns;
            model.Currency = product.Currency;
            model.IsProductPage = isProductPage;
            model.ProductUrl = productUrl;

            // The product's quantity rules and volume discounts.
            if (productPart is not null)
            {
                model.MaxQuantity = productPart.MaxQuantity is > 0 and var max ? Math.Min(model.MaxQuantity, max) : model.MaxQuantity;
                model.MinQuantity = ProductVolumePricing.GetStartingQuantity(productPart);
                model.QuantityStep = Math.Max(1, productPart.QuantityStep ?? 1);
                model.PriceTiers = [.. (productPart.PriceTiers ?? []).Where(tier => tier.MinQuantity > 1 && tier.PercentOff > 0).OrderBy(tier => tier.MinQuantity)];

                var user = _httpContextAccessor.HttpContext?.User;
                model.RoleDiscount = (productPart.RoleDiscounts ?? [])
                    .Where(entry => !string.IsNullOrEmpty(entry.Role) && entry.PercentOff > 0 && user?.IsInRole(entry.Role) == true)
                    .OrderByDescending(entry => entry.PercentOff)
                    .FirstOrDefault();
            }

            // A buyer can't add more than is left.
            if (model.Availability is { CanBuy: true } && stock.GetValueOrDefault(new ProductStockKey(contentItem.ContentItemId, null)) is { AllowBackorder: false } counted)
            {
                model.MaxQuantity = Math.Min(model.MaxQuantity, Math.Max(1, counted.Available));
            }
        }).Location(ProductActionDisplayType, "Actions:5");
    }

    // In stock, Only N left (at or below the product's threshold), Out of stock, or Available on backorder. Exact
    // counts above the threshold stay private.
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
