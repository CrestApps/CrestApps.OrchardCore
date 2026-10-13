namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// How a product's price is shown to visitors.
/// </summary>
public enum ProductPriceDisplay
{
    /// <summary>
    /// The price is shown.
    /// </summary>
    Show = 0,

    /// <summary>
    /// A label such as "Call for price" is shown instead of the price.
    /// </summary>
    OnRequest = 1,

    /// <summary>
    /// Nothing is shown where the price would be.
    /// </summary>
    Hidden = 2,
}
