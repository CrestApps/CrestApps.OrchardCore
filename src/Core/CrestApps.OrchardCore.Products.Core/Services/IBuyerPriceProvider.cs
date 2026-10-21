using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// Gives the buyer their own price for a product, such as from the price list of the company they buy for. The price
/// resolver asks every provider for a one-time price, after converting it to the buyer's currency: the lowest price a
/// provider gives is the price, and volume and role discounts don't come off it too.
/// </summary>
public interface IBuyerPriceProvider
{
    /// <summary>
    /// Returns the buyer's price for each unit, in <see cref="BuyerPriceContext.Currency"/>, or <see langword="null"/>
    /// to leave the price as it is.
    /// </summary>
    ValueTask<decimal?> GetUnitPriceAsync(BuyerPriceContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// The product being priced, and its price before any buyer's own.
/// </summary>
/// <param name="ContentItem">The product.</param>
/// <param name="VariantId">The variant chosen, or <see langword="null"/>.</param>
/// <param name="Sku">The SKU of the product or its variant, or <see langword="null"/>.</param>
/// <param name="UnitPrice">The price for each unit before any buyer's own, in <paramref name="Currency"/>.</param>
/// <param name="Currency">The currency the product is priced in.</param>
/// <param name="Quantity">How many are being bought.</param>
public sealed record BuyerPriceContext(ContentItem ContentItem, string VariantId, string Sku, decimal UnitPrice, string Currency, int Quantity);
