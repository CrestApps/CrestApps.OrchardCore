using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Reconciles a checkout against the payment providers' authoritative APIs. This is the safeguard that
/// prevents orphaned records: the checkout is only ever considered paid when every obligation is backed by
/// a durable <see cref="PaymentAttempt"/> that the provider has independently confirmed succeeded. A
/// cached webhook notification alone never completes a checkout.
/// </summary>
public interface ICheckoutReconciliationService
{
    /// <summary>
    /// Verifies every non-terminal attempt on the session against its provider, records the confirmed
    /// results on the session's payment metadata, and reports whether all obligations are settled.
    /// </summary>
    /// <param name="session">The checkout session to reconcile.</param>
    /// <param name="expectedObligationIds">The obligations the session must settle to be complete.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<CheckoutReconciliationResult> ReconcileAsync(
        CheckoutSession session,
        IEnumerable<string> expectedObligationIds,
        CancellationToken cancellationToken = default);
}
