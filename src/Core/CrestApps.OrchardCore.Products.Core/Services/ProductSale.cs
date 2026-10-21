using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// Works out what a product, or one of its variants, sells for at a moment: its sale price while its sale runs and the
/// sale price is lower, else its price; and the price to show struck through next to it.
/// </summary>
public static class ProductSale
{
    /// <summary>
    /// Returns whether the product's sale window includes the moment. A window with neither end always does.
    /// </summary>
    public static bool IsInWindow(ProductPart product, DateTime utcNow)
        => product is not null &&
            (product.SaleStartsUtc is null || product.SaleStartsUtc <= utcNow) &&
            (product.SaleEndsUtc is null || utcNow < product.SaleEndsUtc);

    /// <summary>
    /// Returns the sale price that applies at the moment, or <see langword="null"/> when the product or variant
    /// isn't on sale then.
    /// </summary>
    public static decimal? GetSalePrice(ProductPart product, ProductVariant variant, DateTime utcNow)
    {
        if (!IsInWindow(product, utcNow))
        {
            return null;
        }

        // A variant priced on its own isn't put on sale by the product's sale price: that price was for the product's.
        var sale = variant?.SalePrice ?? (variant?.Price is null ? product.SalePrice : null);
        var regular = variant?.Price ?? product.Price;

        return sale is { } amount && amount >= 0m && (regular is null || amount < regular) ? amount : null;
    }

    /// <summary>
    /// Returns what the product or variant sells for at the moment.
    /// </summary>
    public static decimal? GetPrice(ProductPart product, ProductVariant variant, DateTime utcNow)
        => GetSalePrice(product, variant, utcNow) ?? variant?.Price ?? product?.Price;

    /// <summary>
    /// Returns the price to show struck through next to it: during a sale, the higher of the compare-at price and
    /// the regular price, so the sale reads as a saving; otherwise the compare-at price.
    /// </summary>
    public static decimal? GetCompareAtPrice(ProductPart product, ProductVariant variant, DateTime utcNow)
    {
        var compareAt = variant?.CompareAtPrice ?? product?.CompareAtPrice;

        if (GetSalePrice(product, variant, utcNow) is null)
        {
            return compareAt;
        }

        var regular = variant?.Price ?? product?.Price;

        return compareAt > regular ? compareAt : regular;
    }

    /// <summary>
    /// Returns whether the product, or any of its variants, has a sale price.
    /// </summary>
    public static bool HasSalePrice(ProductPart product, IEnumerable<ProductVariant> variants)
        => product?.SalePrice is not null || (variants?.Any(variant => variant.SalePrice is not null) ?? false);
}
