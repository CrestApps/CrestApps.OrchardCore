namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// The authoritative details of a Stripe PaymentIntent as reported by the Stripe API. Amounts are the
/// integer minor units Stripe settles in; callers convert them to major units with <see cref="StripeCurrency"/>.
/// </summary>
public sealed class PaymentIntentDetails
{
    /// <summary>
    /// Gets or sets the Stripe PaymentIntent identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the Stripe status of the PaymentIntent (for example <c>succeeded</c> or <c>canceled</c>).
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the amount intended to be collected, in the currency's minor units.
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// Gets or sets the amount actually collected, in the currency's minor units.
    /// </summary>
    public long AmountReceived { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency code of the PaymentIntent.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the PaymentIntent ran against Stripe live mode.
    /// </summary>
    public bool LiveMode { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the latest charge produced by the PaymentIntent, when available.
    /// </summary>
    public string LatestChargeId { get; set; }
}
