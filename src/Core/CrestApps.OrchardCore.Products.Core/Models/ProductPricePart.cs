using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// The ways a product may be bought.
/// </summary>
/// <remarks>
/// Attach this to any product type to sell it on more than one set of terms — a monthly and an annual
/// plan, a one-time purchase alongside a subscription, or a tier the buyer names the amount for. A product
/// without this part is still sellable at the single price on its <see cref="ProductPart"/>.
/// </remarks>
public sealed class ProductPricePart : ContentPart
{
    /// <summary>
    /// Gets or sets the prices this product is offered at.
    /// </summary>
    public IList<ProductPrice> Prices { get; set; } = [];

    /// <summary>
    /// The price the buyer gets when they name none: the one marked default, otherwise the first that can
    /// still be bought.
    /// </summary>
    /// <param name="utcNow">The current UTC time.</param>
    public ProductPrice GetDefault(DateTime utcNow)
    {
        var available = Prices?.Where(price => price.IsAvailable(utcNow)).ToArray() ?? [];

        return Array.Find(available, price => price.IsDefault) ?? available.FirstOrDefault();
    }

    /// <summary>
    /// Finds a price by its identifier, whether or not it can still be bought.
    /// </summary>
    /// <param name="priceId">The price identifier.</param>
    public ProductPrice Find(string priceId)
        => string.IsNullOrEmpty(priceId)
            ? null
            : Prices?.FirstOrDefault(price => string.Equals(price.PriceId, priceId, StringComparison.Ordinal));
}
