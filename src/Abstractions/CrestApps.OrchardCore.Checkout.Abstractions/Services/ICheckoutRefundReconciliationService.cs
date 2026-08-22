using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Reconciles a refund observed at a payment gateway against the durable refund ledger. It is the single
/// authoritative path for applying a remote refund notification: it correlates the notification to an
/// existing <see cref="PaymentRefund"/> by provider reference, idempotency key, or original transaction,
/// updates the local ledger to the gateway's authoritative status, and records a quarantined
/// manual-review entry when a refund exists at the gateway with no matching local request. It never
/// fabricates a refund result the gateway did not confirm.
/// </summary>
public interface ICheckoutRefundReconciliationService
{
    /// <summary>
    /// Reconciles a remote refund notification against the durable refund ledger and returns the affected
    /// ledger entry.
    /// </summary>
    /// <param name="context">The remote refund notification to reconcile.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The created or updated refund ledger entry.</returns>
    Task<PaymentRefund> ReconcileRemoteRefundAsync(ReconcileRemoteRefundContext context, CancellationToken cancellationToken = default);
}
