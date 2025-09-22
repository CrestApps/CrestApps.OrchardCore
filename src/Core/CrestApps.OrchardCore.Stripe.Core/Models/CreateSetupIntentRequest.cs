namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class CreateSetupIntentRequest
{
    public string PaymentMethodId { get; set; }

    public string CustomerId { get; set; }

    public Dictionary<string, string> Metadata { get; set; }
}
