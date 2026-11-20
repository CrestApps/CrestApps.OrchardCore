using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Transactions.Models;
using OrchardCore;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Takes back a payment recorded by hand or an adjustment with a reversing entry: what it paid or took off is owed again,
/// and a transaction it had settled is collectable again. The entry stays on the timeline.
/// </summary>
internal static class TransactionReversal
{
    /// <summary>
    /// Adds the reversing entry of <paramref name="entry"/> to the transaction and changes its amounts and status.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="entry">The payment or adjustment taken back.</param>
    /// <param name="reason">Why it is taken back.</param>
    /// <param name="message">The message of the reversing entry.</param>
    /// <param name="reopenedMessage">The message recorded when the transaction owes money again after being settled; it is
    /// given what is owed again as its only argument.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <returns>The reversing entry.</returns>
    public static TransactionEvent Apply(Transaction transaction, TransactionEvent entry, string reason, string message, Func<string, string> reopenedMessage, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(entry);

        var amount = CurrencyScale.Round(entry.Amount ?? 0m, transaction.Currency);

        if (entry.Type == TransactionEventType.PaymentRecorded)
        {
            transaction.AmountPaid = Math.Max(0m, CurrencyScale.Round(transaction.AmountPaid - amount, transaction.Currency));
        }
        else
        {
            transaction.AmountAdjusted = Math.Max(0m, CurrencyScale.Round(transaction.AmountAdjusted - amount, transaction.Currency));
        }

        transaction.UpdatedUtc = now;

        var reversal = new TransactionEvent
        {
            Id = IdGenerator.GenerateId(),
            CreatedUtc = now,
            Type = TransactionEventType.Reversed,
            Amount = amount,
            Reason = reason,
            ReversesEventId = entry.Id,
            DocumentKind = entry.DocumentKind,
            DocumentId = entry.DocumentId,
            DocumentNumber = entry.DocumentNumber,
            Message = message,
        };

        transaction.Events.Add(reversal);

        // Money is owed again, so a transaction this entry settled is collectable again.
        if (transaction.OutstandingAmount > 0m && transaction.Status == TransactionStatus.Paid)
        {
            transaction.Status = transaction.AmountPaid > 0m ? TransactionStatus.PartiallyPaid : TransactionStatus.Outstanding;
            transaction.SettledUtc = null;
            transaction.SettlementMethod = null;
            transaction.Events.Add(new TransactionEvent
            {
                Id = IdGenerator.GenerateId(),
                CreatedUtc = now,
                Type = TransactionEventType.StatusChanged,
                Message = reopenedMessage($"{CurrencyScale.Format(transaction.OutstandingAmount, transaction.Currency)} {transaction.Currency}"),
            });
        }
        else if (transaction.Status == TransactionStatus.PartiallyPaid && transaction.AmountPaid <= 0m)
        {
            transaction.Status = TransactionStatus.Outstanding;
        }

        return reversal;
    }
}
