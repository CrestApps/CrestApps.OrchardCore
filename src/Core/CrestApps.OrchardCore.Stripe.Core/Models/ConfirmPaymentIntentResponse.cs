namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class ConfirmPaymentIntentResponse
{
    public string Id { get; set; }

    public double? Amount { get; set; }

    public string Currency { get; set; }

    public string CustomerId { get; set; }

    public string Status { get; set; }
}
