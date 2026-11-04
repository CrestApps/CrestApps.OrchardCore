using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Feeds;
using CrestApps.OrchardCore.Seo;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products.StructuredData;

/// <summary>
/// Describes a product with schema.org: a <c>Product</c> with its SKU, brand and identifiers, and its offer (price,
/// currency and availability), or an <c>AggregateOffer</c> with the lowest and highest prices of its variants. A
/// product whose price is hidden gets no offer.
/// </summary>
internal sealed class ProductStructuredDataProvider : ContentPartStructuredDataProvider<ProductPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IEnumerable<IProductAvailabilityProvider> _availabilityProviders;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductStructuredDataProvider"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="availabilityProviders">The providers of product availability.</param>
    /// <param name="clock">The clock.</param>
    public ProductStructuredDataProvider(
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<IProductAvailabilityProvider> availabilityProviders,
        IClock clock)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _availabilityProviders = availabilityProviders;
        _clock = clock;
    }

    /// <inheritdoc/>
    protected override async Task BuildAsync(StructuredDataContext context, ProductPart part)
    {
        var contentItem = context.ContentItem;
        var entity = context.MainEntity;

        context.SuggestType("Product");

        JsonLd.SetIfNotEmpty(entity, "sku", part.Sku);
        JsonLd.SetIfNotEmpty(entity, "productID", part.Sku);

        if (contentItem.TryGet<ProductFeedPart>(out var feedPart))
        {
            if (!string.IsNullOrWhiteSpace(feedPart.Brand))
            {
                entity["brand"] = new JsonObject
                {
                    [StructuredDataConstants.Keywords.Type] = "Brand",
                    ["name"] = feedPart.Brand.Trim(),
                };
            }

            JsonLd.SetIfNotEmpty(entity, "gtin", feedPart.Gtin);
            JsonLd.SetIfNotEmpty(entity, "mpn", feedPart.Mpn);
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);
        var settings = definition?.Parts
            .FirstOrDefault(typePart => typePart.PartDefinition.Name == nameof(ProductPart))?
            .GetSettings<ProductPartSettings>() ?? new ProductPartSettings();

        if (part.GetPriceDisplay(settings) != ProductPriceDisplay.Show)
        {
            return;
        }

        var now = _clock.UtcNow;
        var condition = ToItemCondition(feedPart?.Condition);
        var variants = contentItem.TryGet<ProductVariantsPart>(out var variantsPart)
            ? variantsPart.GetAvailable().ToList()
            : [];

        var keys = new List<ProductStockKey>
        {
            new(contentItem.ContentItemId, null),
        };

        keys.AddRange(variants.Select(variant => new ProductStockKey(contentItem.ContentItemId, variant.VariantId)));

        var availability = await _availabilityProviders.GetAvailabilityAsync(keys);
        var productStock = availability.GetValueOrDefault(new ProductStockKey(contentItem.ContentItemId, null));
        var canBuy = part.CanBePurchased(settings);

        if (variants.Count == 0)
        {
            var price = GetPrice(contentItem, part, variant: null, now);

            if (price is null)
            {
                return;
            }

            entity["offers"] = BuildOffer(context, part, price.Value.Amount, price.Value.Currency, productStock, canBuy, condition, now);

            return;
        }

        var prices = variants
            .Select(variant => GetPrice(contentItem, part, variant, now))
            .Where(price => price is not null)
            .Select(price => price.Value)
            .ToList();

        if (prices.Count == 0)
        {
            return;
        }

        var anyInStock = variants.Any(variant => IsAvailable(availability.GetValueOrDefault(new ProductStockKey(contentItem.ContentItemId, variant.VariantId)) ?? productStock));

        var offer = new JsonObject
        {
            [StructuredDataConstants.Keywords.Type] = "AggregateOffer",
            ["lowPrice"] = FormatPrice(prices.Min(price => price.Amount)),
            ["highPrice"] = FormatPrice(prices.Max(price => price.Amount)),
            ["offerCount"] = variants.Count,
            ["priceCurrency"] = prices[0].Currency,
            ["availability"] = canBuy && anyInStock ? "https://schema.org/InStock" : "https://schema.org/OutOfStock",
        };

        JsonLd.SetIfNotEmpty(offer, "url", context.Url);
        entity["offers"] = offer;
    }

    /// <summary>
    /// Gets the price anyone pays for a product or one of its variants, without a signed-in buyer's own price: the sale
    /// price while a sale is on, otherwise the regular price.
    /// </summary>
    /// <param name="contentItem">The product content item.</param>
    /// <param name="part">The product part.</param>
    /// <param name="variant">The variant, or <see langword="null"/> for the product.</param>
    /// <param name="utcNow">The current time.</param>
    /// <returns>The price and its currency, or <see langword="null"/> when the product has no price.</returns>
    internal static (decimal Amount, string Currency)? GetPrice(ContentItem contentItem, ProductPart part, ProductVariant variant, DateTime utcNow)
    {
        var listed = contentItem.TryGet<ProductPricePart>(out var pricePart) ? pricePart.GetDefault(utcNow) : null;

        if (listed is { Kind: PriceKind.Recurring })
        {
            return null;
        }

        var regular = variant?.Price ?? part.Price ?? listed?.Amount;

        if (regular is null)
        {
            return null;
        }

        var sale = ProductSale.GetSalePrice(part, variant, utcNow);
        var currency = variant?.Price is null && part.Price is null && !string.IsNullOrEmpty(listed?.Currency)
            ? listed.Currency
            : part.Currency;

        if (string.IsNullOrEmpty(currency))
        {
            return null;
        }

        var amount = sale is { } salePrice && salePrice < regular.Value ? salePrice : regular.Value;

        return (amount, currency.ToUpperInvariant());
    }

    /// <summary>
    /// Maps a product condition, as the product feeds write it, to its schema.org value.
    /// </summary>
    /// <param name="condition">The condition: <c>new</c>, <c>used</c> or <c>refurbished</c>.</param>
    /// <returns>The schema.org item condition, or <see langword="null"/> when unknown.</returns>
    internal static string ToItemCondition(string condition)
    {
        return condition?.Trim().ToLowerInvariant() switch
        {
            "new" => "https://schema.org/NewCondition",
            "used" => "https://schema.org/UsedCondition",
            "refurbished" => "https://schema.org/RefurbishedCondition",
            _ => null,
        };
    }

    /// <summary>
    /// Maps a product's availability to its schema.org value.
    /// </summary>
    /// <param name="availability">The availability, or <see langword="null"/> when no provider tracks it.</param>
    /// <param name="canBuy">Whether the product can be bought on the site.</param>
    /// <returns>The schema.org availability.</returns>
    internal static string ToAvailability(ProductAvailability availability, bool canBuy)
    {
        if (!canBuy)
        {
            return "https://schema.org/OutOfStock";
        }

        return availability?.Status switch
        {
            null or ProductAvailabilityStatus.InStock => "https://schema.org/InStock",
            ProductAvailabilityStatus.LowStock => "https://schema.org/LimitedAvailability",
            ProductAvailabilityStatus.Backorder => "https://schema.org/BackOrder",
            ProductAvailabilityStatus.Preorder => "https://schema.org/PreOrder",
            _ => "https://schema.org/OutOfStock",
        };
    }

    private static JsonObject BuildOffer(StructuredDataContext context, ProductPart part, decimal amount, string currency, ProductAvailability availability, bool canBuy, string condition, DateTime utcNow)
    {
        var offer = new JsonObject
        {
            [StructuredDataConstants.Keywords.Type] = "Offer",
            ["price"] = FormatPrice(amount),
            ["priceCurrency"] = currency,
            ["availability"] = ToAvailability(availability, canBuy),
        };

        JsonLd.SetIfNotEmpty(offer, "url", context.Url);
        JsonLd.SetIfNotEmpty(offer, "sku", part.Sku);
        JsonLd.SetIfNotEmpty(offer, "itemCondition", condition);

        if (part.SaleEndsUtc is { } saleEnds && ProductSale.GetSalePrice(part, null, utcNow) is not null)
        {
            offer["priceValidUntil"] = saleEnds.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return offer;
    }

    private static bool IsAvailable(ProductAvailability availability)
        => availability?.Status is null or ProductAvailabilityStatus.InStock or ProductAvailabilityStatus.LowStock or ProductAvailabilityStatus.Backorder or ProductAvailabilityStatus.Preorder;

    private static string FormatPrice(decimal amount)
        => amount.ToString("0.00", CultureInfo.InvariantCulture);
}
