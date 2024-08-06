namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class UpdateProductRequest
{
    public string Title { get; set; }

    public bool? IsActive { get; set; }

    public string Description { get; set; }
}
