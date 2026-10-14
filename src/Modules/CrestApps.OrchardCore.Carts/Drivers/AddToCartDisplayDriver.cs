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
                    Price = (variant.Price ?? productPrice) is { } amount && !string.IsNullOrEmpty(product.Currency)
                        ? ProductPriceFormatter.Format(amount, product.Currency)
                        : null,
                    Options = new Dictionary<string, string>(variant.Options, StringComparer.OrdinalIgnoreCase),
                    ImagePath = variant.ImagePath,
                })
                .ToList();

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
        }).Location(ProductActionDisplayType, "Actions:5");
    }
}
