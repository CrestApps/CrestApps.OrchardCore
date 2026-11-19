namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// The price checks of the product editor (<c>ProductPartDisplayDriver</c>), shared with the product AI tools: a price
/// when one is needed, no negative price, compare-at price or sale price, a sale price below the price, and a sale that
/// ends after it starts. The checks return what is wrong; each caller words it.
/// </summary>
internal static class ProductPricingRules
{
    /// <summary>
    /// Checks a product's price fields as they would be saved.
    /// </summary>
    /// <param name="price">The price.</param>
    /// <param name="priceRequired">Whether the product needs a price: it can be bought or its price is shown.</param>
    /// <param name="compareAtPrice">The compare-at price.</param>
    /// <param name="salePrice">The sale price.</param>
    /// <param name="saleStarts">When the sale starts, in any one time zone.</param>
    /// <param name="saleEnds">When the sale ends, in the same time zone.</param>
    /// <returns>What is wrong, in the editor's order; empty when nothing is.</returns>
    public static IReadOnlyList<ProductPriceProblem> Check(decimal? price, bool priceRequired, decimal? compareAtPrice, decimal? salePrice, DateTime? saleStarts, DateTime? saleEnds)
    {
        var problems = new List<ProductPriceProblem>();

        if (!price.HasValue && priceRequired)
        {
            problems.Add(ProductPriceProblem.PriceRequired);
        }
        else if (price < 0)
        {
            problems.Add(ProductPriceProblem.NegativePrice);
        }

        if (compareAtPrice < 0)
        {
            problems.Add(ProductPriceProblem.NegativeCompareAtPrice);
        }

        if (salePrice < 0)
        {
            problems.Add(ProductPriceProblem.NegativeSalePrice);
        }
        else if (salePrice.HasValue && price.HasValue && salePrice >= price)
        {
            problems.Add(ProductPriceProblem.SaleNotBelowPrice);
        }

        if (saleStarts.HasValue && saleEnds.HasValue && saleEnds <= saleStarts)
        {
            problems.Add(ProductPriceProblem.SaleEndsBeforeStart);
        }

        return problems;
    }

    /// <summary>
    /// Normalizes a currency code as the product editor stores it: trimmed and in capitals, or <see langword="null"/>.
    /// </summary>
    /// <param name="currencyCode">The code entered.</param>
    /// <returns>The code to store.</returns>
    public static string NormalizeCurrencyCode(string currencyCode)
        => string.IsNullOrWhiteSpace(currencyCode)
            ? null
            : currencyCode.Trim().ToUpperInvariant();
}

/// <summary>
/// What is wrong with a product's price fields.
/// </summary>
internal enum ProductPriceProblem
{
    /// <summary>
    /// The product can be bought or shows its price, and has none.
    /// </summary>
    PriceRequired,

    /// <summary>
    /// The price is below zero.
    /// </summary>
    NegativePrice,

    /// <summary>
    /// The compare-at price is below zero.
    /// </summary>
    NegativeCompareAtPrice,

    /// <summary>
    /// The sale price is below zero.
    /// </summary>
    NegativeSalePrice,

    /// <summary>
    /// The sale price is not lower than the price.
    /// </summary>
    SaleNotBelowPrice,

    /// <summary>
    /// The sale ends when or before it starts.
    /// </summary>
    SaleEndsBeforeStart,
}
