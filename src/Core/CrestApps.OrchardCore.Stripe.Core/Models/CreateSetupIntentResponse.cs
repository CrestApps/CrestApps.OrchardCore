namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class CreateSetupIntentResponse
{
    public string Id { get; set; }

    public string Status { get; set; }

    public string ClientSecret { get; set; }
}
