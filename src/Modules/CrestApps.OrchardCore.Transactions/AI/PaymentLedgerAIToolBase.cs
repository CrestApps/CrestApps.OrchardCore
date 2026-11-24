using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// A tool of the Payments, Refunds and Payment issues ledgers: it needs
/// <see cref="TransactionsPermissions.ManageRefunds"/>, as those pages.
/// </summary>
internal abstract class PaymentLedgerAIToolBase : TransactionsAIToolBase
{
    /// <inheritdoc/>
    protected override Permission Permission => TransactionsPermissions.ManageRefunds;

    /// <summary>
    /// Gets everything already given back against a payment, settled or still in flight, as the payment's refund form
    /// counts it: counting an in-flight refund keeps a second refund from being started for money already on its way.
    /// </summary>
    /// <param name="refundStore">The refund store.</param>
    /// <param name="attempt">The payment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The amount and the refunds of the payment.</returns>
    protected static async Task<(decimal Refunded, IReadOnlyList<PaymentRefund> Refunds)> GetRefundedAsync(IPaymentRefundStore refundStore, PaymentAttempt attempt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(attempt.TransactionId))
        {
            return (0m, []);
        }

        var refunds = (await refundStore.GetByOriginalTransactionAsync(attempt.TransactionId, cancellationToken)).ToList();
        var total = refunds
            .Where(refund => refund.Status is not (RefundStatus.Failed or RefundStatus.Canceled))
            .Sum(refund => refund.RefundGrossAmount);

        return (CurrencyScale.Round(total, attempt.Currency), refunds);
    }

    /// <summary>
    /// Gets what a payment settled for, tax included.
    /// </summary>
    /// <param name="attempt">The payment.</param>
    /// <returns>The settled amount.</returns>
    protected static decimal Settled(PaymentAttempt attempt)
        => attempt.ConfirmedAmount + attempt.ConfirmedTaxAmount;
}
