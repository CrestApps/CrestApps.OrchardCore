namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// The lifecycle state of a durable <see cref="PaymentRefund"/>. Like a <see cref="PaymentAttempt"/>, a
/// refund is written to the tenant database <em>before</em> the provider is called so a crash or node
/// failure can never strand a real refund as an unrecorded action, and duplicate requests are collapsed
/// by the refund's idempotency key.
/// </summary>
public enum RefundStatus
{
    /// <summary>
    /// The refund has been persisted but the provider has not been called yet.
    /// </summary>
    Requested,

    /// <summary>
    /// The provider has been called and the outcome is not yet confirmed.
    /// </summary>
    Pending,

    /// <summary>
    /// The provider confirmed the refund succeeded.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The provider reported the refund failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The refund was canceled before it was submitted to the provider.
    /// </summary>
    Canceled,

    /// <summary>
    /// The refund cannot be processed automatically (for example the provider has no executable refund
    /// operation) and must be settled manually by an operator.
    /// </summary>
    PendingManualReview,
}
