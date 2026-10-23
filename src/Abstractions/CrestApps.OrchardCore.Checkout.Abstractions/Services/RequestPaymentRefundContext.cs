namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The input to <see cref="ICheckoutRefundService.RequestRefundAsync"/>. It identifies the settled payment
/// to refund and how much to refund, and the service resolves the owning provider, tax allocation, and
/// remaining refundable amount from the durable ledger.
/// </summary>
public sealed class RequestPaymentRefundContext
{
    /// <summary>
    /// The checkout session the original payment belongs to.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// The provider transaction identifier of the original payment being refunded (the
    /// <c>PaymentRecord</c> key, for example a Stripe PaymentIntent id).
    /// </summary>
    public string OriginalTransactionId { get; set; }

    /// <summary>
    /// The gross amount to refund, including tax, in major currency units. Leave <see langword="null"/> to
    /// refund the full remaining refundable amount of the original payment.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// The operator- or customer-supplied reason for the refund.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the kind of thing the refund gives money back for, when the original payment paid for several
    /// (see <see cref="CheckoutSession.References"/>). Leave it empty when the payment paid for one thing.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the thing the refund gives money back for, when the original payment paid for
    /// several, for example the one transaction of a checkout that settled several. It is copied onto the refund, so
    /// the refund is recorded against that thing only.
    /// </summary>
    public string ReferenceId { get; set; }
}
