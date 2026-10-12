namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents the product values to update in Stripe.
/// </summary>
public class UpdateProductRequest
{
    /// <summary>
    /// Gets or sets the updated Stripe product name.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the product should be active in Stripe.
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Gets or sets the updated Stripe product description.
    /// </summary>
    public string Description { get; set; }
}
