namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class CreateSetupIntentResponse
{
    public string Status { get; set; }

    public string ClientSecret { get; set; }

    public string CustomerId { get; set; }
}
