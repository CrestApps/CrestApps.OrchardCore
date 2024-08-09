namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class CreatePaymentIntentResponse
{
    public string ClientSecret { get; set; }

    public string CustomerId { get; set; }

    public string Status { get; set; }
}
