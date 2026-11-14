namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Declares what a payment provider can do so the checkout framework can pick a suitable provider,
/// enforce constraints (for example refusing to add a separate up-front fee to a provider-hosted page
/// that cannot represent one), and decide whether the checkout must compute tax itself or defer to the
/// provider.
/// </summary>
public sealed class PaymentProviderCapabilities
{
    /// <summary>
    /// Whether the provider can charge a single, one-time amount.
    /// </summary>
    public bool SupportsOneTimePayments { get; set; }

    /// <summary>
    /// Whether the provider can establish recurring billing.
    /// </summary>
    public bool SupportsRecurringPayments { get; set; }

    /// <summary>
    /// Whether the provider collects payment on its own hosted page the customer is redirected to.
    /// </summary>
    public bool SupportsHostedCheckout { get; set; }

    /// <summary>
    /// Whether the provider collects payment through in-page elements embedded on the checkout page.
    /// </summary>
    public bool SupportsEmbeddedElements { get; set; }

    /// <summary>
    /// Whether the provider can combine a one-time amount and recurring billing in a single interaction.
    /// When <c>false</c>, the checkout must settle one-time and recurring obligations separately.
    /// </summary>
    public bool SupportsCombinedOneTimeAndRecurring { get; set; }

    /// <summary>
    /// Whether the provider computes and collects tax itself. When <c>true</c> the checkout supplies tax
    /// context and does not add its own tax line; when <c>false</c> the checkout's <see cref="ICheckoutTaxService"/>
    /// determines tax and folds it into the charged amount.
    /// </summary>
    public bool CollectsTaxDynamically { get; set; }

    /// <summary>
    /// Whether the provider supports refunding a settled payment.
    /// </summary>
    public bool SupportsRefunds { get; set; }

    /// <summary>
    /// Whether the provider can keep the payment method used for a payment so it can be charged again later
    /// without the payer present, and can take such a charge. A provider that sets this implements
    /// <see cref="ICheckoutSavedPaymentMethodProvider"/> and honors the <see cref="CheckoutPaymentDataKeys"/>.
    /// </summary>
    public bool SupportsSavedPaymentMethods { get; set; }

    /// <summary>
    /// Whether the provider records a promise to pay later instead of collecting money now, as Pay Later does.
    /// Such a payment creates a new balance to collect; it cannot pay off a balance that already exists, so it is
    /// neither offered nor accepted on a checkout that settles one (<see cref="CheckoutSession.SettlesExistingBalance"/>).
    /// </summary>
    public bool DefersPayment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider charges, on the server and with no browser, a payment
    /// credential an AI agent brought (<see cref="CheckoutPaymentDataKeys.DelegatedCredential"/>).
    /// </summary>
    public bool AcceptsDelegatedCredentials { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider charges a card with the result of a 3-D Secure
    /// authentication run outside it (<see cref="ThreeDSecureResult"/>, in the <c>ThreeDSecure…</c> keys of
    /// <see cref="CheckoutPaymentDataKeys"/>) instead of running its own. When it began the same attempt earlier and the
    /// bank asked for authentication, it finishes that payment with the result rather than starting another.
    /// </summary>
    public bool AcceptsExternalAuthentication { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the buyer enters a card online, with no card present, so the provider can
    /// be used to test stolen cards. The card-testing protection counts such a provider's declines and stops a network,
    /// a customer or the store when they pile up. A card reader, a cash or cheque method, or a promise to pay later is not.
    /// </summary>
    public bool IsCardNotPresent { get; set; }
}
