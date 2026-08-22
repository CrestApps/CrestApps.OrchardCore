using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a request to create a Stripe product from a local product definition.
/// </summary>
public class CreateProductRequest
{
    /// <summary>
    /// Gets or sets the Stripe product identifier to assign.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the product title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the product description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the local product type used to choose the Stripe product type.
    /// </summary>
    public ProductType Type { get; set; }
}
