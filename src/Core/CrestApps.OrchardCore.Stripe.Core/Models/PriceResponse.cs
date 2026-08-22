namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents Stripe price details used by product and subscription synchronization.
/// </summary>
public class PriceResponse
{
    /// <summary>
    /// Gets or sets the Stripe price identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the stable lookup key used to find the price in Stripe.
    /// </summary>
    public string LookupKey { get; set; }

    /// <summary>
    /// Gets or sets the Stripe product identifier associated with the price.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Gets or sets the display title stored as the Stripe price nickname.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the price is active in Stripe.
    /// </summary>
    public bool IsActive { get; set; }
}
