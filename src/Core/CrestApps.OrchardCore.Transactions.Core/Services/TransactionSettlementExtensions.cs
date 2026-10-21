using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// Applies money and settlement to a transaction the same way from every place that takes a payment or lowers what is
/// owed: an online payment, an offline one, marking it paid, or an adjustment.
/// </summary>
public static class TransactionSettlementExtensions
{
    /// <summary>
    /// Adds a payment to what was paid. The transaction is settled when nothing is left to pay, and partly paid
    /// otherwise.
    /// </summary>
    /// <param name="transaction">The transaction paid.</param>
    /// <param name="amount">The money applied, in the transaction's currency.</param>
    /// <param name="settlementMethod">How it was paid, one of the <see cref="TransactionsConstants.SettlementMethods"/>.</param>
    /// <param name="utcNow">When the payment was applied.</param>
    /// <param name="settledMessage">The timeline message recorded when the payment settles the transaction.</param>
    /// <returns>Whether the payment settled the transaction.</returns>
    public static bool ApplyPayment(this Transaction transaction, decimal amount, string settlementMethod, DateTime utcNow, string settledMessage)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        transaction.AmountPaid = CurrencyScale.Round(transaction.AmountPaid + amount, transaction.Currency);
        transaction.SettlementMethod = settlementMethod;
        transaction.UpdatedUtc = utcNow;

        if (transaction.TrySettle(utcNow, settledMessage))
        {
            return true;
        }

        if (transaction.AmountPaid > 0m)
        {
            transaction.Status = TransactionStatus.PartiallyPaid;
        }

        return false;
    }

    /// <summary>
    /// Settles the transaction when nothing is left to pay: it is paid, settled now, and the timeline says so.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="utcNow">When it was settled.</param>
    /// <param name="settledMessage">The timeline message recorded when it is settled.</param>
    /// <returns>Whether the transaction was settled.</returns>
    public static bool TrySettle(this Transaction transaction, DateTime utcNow, string settledMessage)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.OutstandingAmount > 0m)
        {
            return false;
        }

        transaction.Status = TransactionStatus.Paid;
        transaction.SettledUtc = utcNow;
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = utcNow,
            Type = TransactionEventType.StatusChanged,
            Message = settledMessage,
        });

        return true;
    }
}
