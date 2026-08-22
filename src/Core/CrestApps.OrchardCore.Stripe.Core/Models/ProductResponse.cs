namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents Stripe product details returned by product operations.
/// </summary>
public class ProductResponse
{
    /// <summary>
    /// Gets or sets the Stripe product identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the Stripe product name.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the Stripe product description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the Stripe product type, such as <c>good</c> or <c>service</c>.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the product is active in Stripe.
    /// </summary>
    public bool IsActive { get; set; }
}
