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
}
