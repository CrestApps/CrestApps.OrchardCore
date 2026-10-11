namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a request to refund a settled Stripe PaymentIntent. The amount is expressed in major
/// currency units; the refund service converts it to the currency's minor units with
/// <see cref="StripeCurrency"/> so no call site performs a hardcoded conversion.
/// </summary>
public sealed class CreateRefundRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the Stripe PaymentIntent identifier of the payment being refunded.
    /// </summary>
    public string PaymentIntentId { get; set; }

    /// <summary>
    /// Gets or sets the gross amount to refund in major currency units, including tax.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency code of the refund.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the optional Stripe refund reason (for example <c>requested_by_customer</c>).
    /// </summary>
    public string Reason { get; set; }
}
