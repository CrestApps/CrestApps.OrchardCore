namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents the price values to update in Stripe.
/// </summary>
public class UpdatePriceRequest
{
    /// <summary>
    /// Gets or sets the updated display title stored as the Stripe price nickname.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the price should be active in Stripe.
    /// </summary>
    public bool? IsActive { get; set; }
}
