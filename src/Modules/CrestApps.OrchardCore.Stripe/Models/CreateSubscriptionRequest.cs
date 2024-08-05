namespace CrestApps.OrchardCore.Stripe.Models;

public class CreateSubscriptionRequest
{
    public string CustomerId { get; set; }

    public string PaymentMethodId { get; set; }

    public string PlanId { get; set; }

    public Dictionary<string, string> Metadata { get; set; }
}
