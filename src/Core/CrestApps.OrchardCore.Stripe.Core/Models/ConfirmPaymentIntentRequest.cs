namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class ConfirmPaymentIntentRequest
{
    public string PaymentMethodId { get; set; }

    public string PaymentIntentId { get; set; }
}
