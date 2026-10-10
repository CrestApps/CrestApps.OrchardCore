using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Reacts to a refund that completed: money that went back to the payer, however the refund finished. It finishes
/// when the gateway confirms it straight away, when a gateway notification confirms it later, or when an
/// administrator records a refund that was paid back by hand. It is the one place to tell the customer, so that does
/// not depend on which of those happened.
/// </summary>
/// <remarks>
/// Handlers run once per refund, after it is saved as succeeded. A handler that fails is logged and does not undo the
/// refund or stop the other handlers.
/// </remarks>
public interface IPaymentRefundHandler
{
    /// <summary>
    /// Called after a refund succeeded.
    /// </summary>
    /// <param name="refund">The refund, as saved.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RefundSucceededAsync(PaymentRefund refund, CancellationToken cancellationToken = default);
}
