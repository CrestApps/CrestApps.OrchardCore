namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class UpdatePriceRequest
{
    public string Title { get; set; }

    public bool? IsActive { get; set; }
}
