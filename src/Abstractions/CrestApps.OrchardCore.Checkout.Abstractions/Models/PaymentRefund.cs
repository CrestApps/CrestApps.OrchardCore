using CrestApps.Core.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// A durable, per-refund record of a single refund of a settled payment. It is persisted in the tenant
/// database (never only in a distributed cache) and is written before the provider is called, so a refund
/// is never lost or double-applied even across distributed nodes. Amounts are carried as
/// <see cref="decimal"/> — the authoritative representation for durable financial records — and are only
/// converted to a provider's integer minor units at the gateway boundary.
/// </summary>
public sealed class PaymentRefund : CatalogItem
{
    /// <summary>
    /// The YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The checkout session the original payment belongs to.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// The key of the payment provider that processed the original payment and owns the refund.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// The identifier of the original <see cref="PaymentAttempt"/> being refunded, when known.
    /// </summary>
    public string OriginalAttemptId { get; set; }

    /// <summary>
    /// The provider transaction identifier of the original payment being refunded (for example a Stripe
    /// PaymentIntent id). Used to correlate every refund of the same payment.
    /// </summary>
    public string OriginalTransactionId { get; set; }

    /// <summary>
    /// The obligation the original payment settled, when applicable.
    /// </summary>
    public string ObligationId { get; set; }

    /// <summary>
    /// The provider's authoritative reference for this refund (for example a Stripe refund id). Stored as
    /// soon as the provider returns it so the remote refund is never lost.
    /// </summary>
    public string ProviderRefundReference { get; set; }

    /// <summary>
    /// The ISO-4217 currency code of the refund.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// The gross amount being refunded, including tax, in major currency units.
    /// </summary>
    public decimal RefundGrossAmount { get; set; }

    /// <summary>
    /// The tax portion of <see cref="RefundGrossAmount"/>, derived from the original payment's immutable
    /// tax snapshot and never recalculated with current rules.
    /// </summary>
    public decimal RefundTaxAmount { get; set; }

    /// <summary>
    /// The portion of the original taxable base being refunded.
    /// </summary>
    public decimal RefundTaxableAmount { get; set; }

    /// <summary>
    /// The per-jurisdiction refunded tax lines, allocated from the original payment's tax snapshot so each
    /// tax is refunded according to the original determination.
    /// </summary>
    public IList<TaxLine> TaxLines { get; set; } = [];

    /// <summary>
    /// The current lifecycle state of the refund.
    /// </summary>
    public RefundStatus Status { get; set; }

    /// <summary>
    /// The operator- or customer-supplied reason for the refund.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// The provider failure code, when <see cref="Status"/> is <see cref="RefundStatus.Failed"/>.
    /// </summary>
    public string FailureCode { get; set; }

    /// <summary>
    /// The provider failure reason, when <see cref="Status"/> is <see cref="RefundStatus.Failed"/>.
    /// </summary>
    public string FailureReason { get; set; }

    /// <summary>
    /// The deterministic idempotency key sent to the provider so a retried refund never double-refunds.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// The provider mode the refund ran in (test or live).
    /// </summary>
    public GatewayMode GatewayMode { get; set; }

    /// <summary>
    /// The UTC time the refund was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The UTC time the refund was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// The UTC time the refund reached a terminal state, when applicable.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
}
