namespace CrestApps.OrchardCore.Stripe.Core;

public sealed class StripeOptions
{
    public bool IsLive { get; set; }

    public string PublishableKey { get; set; }

    public string ApiKey { get; set; }

    public string WebhookSecret { get; set; }

    /// <summary>
    /// The Stripe integration model used to collect payment. Defaults to
    /// <see cref="StripeCheckoutMode.PaymentElements"/> for backward compatibility.
    /// </summary>
    public StripeCheckoutMode CheckoutMode { get; set; } = StripeCheckoutMode.PaymentElements;
}
