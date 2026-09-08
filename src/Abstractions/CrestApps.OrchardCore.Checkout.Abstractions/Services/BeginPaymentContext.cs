using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The input for <see cref="ICheckoutPaymentProvider.BeginAsync"/>. It carries the durable attempt the
/// provider must settle along with the return/cancel URLs for hosted-redirect flows.
/// </summary>
public sealed class BeginPaymentContext
{
    /// <summary>
    /// The checkout session the payment belongs to.
    /// </summary>
    public CheckoutSession Session { get; set; }

    /// <summary>
    /// The durable attempt the provider must settle. It is already persisted before the provider is called.
    /// </summary>
    public PaymentAttempt Attempt { get; set; }

    /// <summary>
    /// The invoice describing the amounts and tax for the checkout.
    /// </summary>
    public CheckoutInvoice Invoice { get; set; }

    /// <summary>
    /// The absolute URL the provider should return the customer to after a successful hosted payment.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// The absolute URL the provider should return the customer to when they cancel a hosted payment.
    /// </summary>
    public string CancelUrl { get; set; }

    /// <summary>
    /// The opaque values the client handed over when it asked to begin, such as a tokenized payment method.
    /// </summary>
    /// <remarks>
    /// A checkout that also has a recurring obligation tokenizes one reusable payment method in the browser
    /// and confirms every obligation with it. The one-time provider therefore has to see the same data the
    /// recurring one does, or it would create a charge the prepared payment method cannot settle.
    /// </remarks>
    public IReadOnlyDictionary<string, string> ProviderData { get; set; }
}
