namespace CrestApps.OrchardCore.Stripe.ViewModels;

/// <summary>
/// The data the Stripe panel needs to mount its card element on the generic checkout payment step.
/// </summary>
public sealed class StripeCheckoutPaymentMethodViewModel
{
    /// <summary>
    /// Gets or sets the checkout session the payment belongs to.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the publishable key Stripe.js is initialized with. It is safe to render: it can only
    /// create payment methods, never read or move money.
    /// </summary>
    public string PublishableKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tenant is connected to live Stripe credentials, so a test
    /// configuration can be labelled as such on the page.
    /// </summary>
    public bool IsLive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the checkout includes something billed on a recurring cycle.
    /// </summary>
    /// <remarks>
    /// A recurring agreement has to be created against a reusable payment method, so the browser tokenizes
    /// the card before the payment starts. A checkout with nothing recurring skips that round trip.
    /// </remarks>
    public bool HasRecurringItems { get; set; }
}
