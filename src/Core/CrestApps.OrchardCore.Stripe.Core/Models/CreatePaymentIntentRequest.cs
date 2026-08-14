namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class CreatePaymentIntentRequest : StripeWriteRequest
{
    public string PaymentMethodId { get; set; }

    public string CustomerId { get; set; }

    public double? Amount { get; set; }

    public string Currency { get; set; }

    public Dictionary<string, string> Metadata { get; set; }
}
