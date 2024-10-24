using CrestApps.OrchardCore.Payments.Models;

namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class CreateSubscriptionRequest
{
    public string CustomerId { get; set; }

    public string PaymentMethodId { get; set; }

    public Dictionary<string, string> Metadata { get; set; }

    public IList<SubscriptionLineItem> LineItems { get; set; }

    public int? TrialDuration { get; set; }

    public int? BillingCycles { get; set; }

    public DurationType TrialDurationType { get; set; }
}
