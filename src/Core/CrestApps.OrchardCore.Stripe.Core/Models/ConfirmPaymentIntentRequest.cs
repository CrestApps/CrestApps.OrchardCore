namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class ConfirmPaymentIntentRequest : StripeWriteRequest
{
    public string PaymentMethodId { get; set; }

    public string PaymentIntentId { get; set; }
}
