using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// Raises <see cref="ITransactionPaymentHandler.PaymentRecordedAsync"/> the same way from every place a payment is
/// applied, so a receipt or a payment plan never depends on which screen or process took the money.
/// </summary>
public static class TransactionPaymentHandlerExtensions
{
    /// <summary>
    /// Tells every handler that the payments in <paramref name="payments"/> were applied to the transaction. A
    /// handler that throws is logged and the rest still run: the payment is already saved and must not be undone
    /// because a receipt could not be sent.
    /// </summary>
    /// <param name="handlers">The registered handlers.</param>
    /// <param name="transaction">The transaction, as saved with the payments applied.</param>
    /// <param name="payments">The timeline events that recorded the payments.</param>
    /// <param name="logger">The logger for handler failures.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static async Task PaymentRecordedAsync(
        this IEnumerable<ITransactionPaymentHandler> handlers,
        Transaction transaction,
        IEnumerable<TransactionEvent> payments,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (handlers is null || payments is null)
        {
            return;
        }

        foreach (var payment in payments)
        {
            var context = new TransactionPaymentRecordedContext(transaction, payment);

            foreach (var handler in handlers)
            {
                try
                {
                    await handler.PaymentRecordedAsync(context, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "A payment handler '{Handler}' failed for transaction '{TransactionId}'; the payment itself stays recorded.", handler.GetType().Name, transaction.ItemId);
                }
            }
        }
    }

    /// <summary>
    /// Creates the timeline event that records a payment, with the identifier, amount and method the payment
    /// handlers and receipts rely on.
    /// </summary>
    /// <param name="createdUtc">When the payment was applied.</param>
    /// <param name="amount">The money applied, in the transaction's currency.</param>
    /// <param name="method">How it was paid, one of the <see cref="TransactionsConstants.SettlementMethods"/>.</param>
    /// <param name="message">The human-readable description.</param>
    public static TransactionEvent CreatePaymentEvent(DateTime createdUtc, decimal amount, string method, string message)
        => new()
        {
            Id = IdGenerator.GenerateId(),
            CreatedUtc = createdUtc,
            Type = TransactionEventType.PaymentRecorded,
            Amount = amount,
            Method = method,
            Message = message,
        };
}
