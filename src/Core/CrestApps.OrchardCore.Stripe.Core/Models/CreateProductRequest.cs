using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class CreateProductRequest
{
    public string Id { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public ProductType Type { get; set; }
}
