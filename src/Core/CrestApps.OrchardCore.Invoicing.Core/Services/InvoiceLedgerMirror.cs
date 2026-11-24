using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Copies onto an issued invoice what its transaction holds: what was paid (less what was refunded), what is still due,
/// whether it is settled, and a history entry for each payment, payment taken back and refund. The ledger is the record;
/// the invoice only mirrors it, so it can always be worked out again from the transaction.
/// </summary>
public static class InvoiceLedgerMirror
{
    /// <summary>
    /// Mirrors the transaction onto the invoice. Returns whether anything changed.
    /// </summary>
    /// <param name="invoice">The issued invoice.</param>
    /// <param name="transaction">The transaction it posted.</param>
    /// <param name="credited">What issued credit notes credit on the invoice.</param>
    /// <param name="S">The localizer of the history entries.</param>
    public static bool Apply(Invoice invoice, Transaction transaction, decimal credited, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(S);

        var changed = false;
        var paid = Math.Max(0m, transaction.AmountPaid - transaction.AmountRefunded);
        var due = transaction.Status == TransactionStatus.Canceled || invoice.Status == InvoiceStatus.Void
            ? 0m
            : transaction.OutstandingAmount;
        var total = invoice.Totals?.Total ?? 0m;

        // Settled is about money received and credited, never about what was written off.
        var settlement = paid + credited >= total && total > 0m
            ? InvoiceSettlement.Paid
            : paid > 0m || credited > 0m
                ? InvoiceSettlement.PartiallyPaid
                : InvoiceSettlement.Unpaid;

        if (total <= 0m)
        {
            settlement = InvoiceSettlement.Paid;
        }

        changed |= Set(invoice.AmountPaid, paid, value => invoice.AmountPaid = value);
        changed |= Set(invoice.AmountCredited, credited, value => invoice.AmountCredited = value);
        changed |= Set(invoice.AmountDue, due, value => invoice.AmountDue = value);

        if (invoice.Settlement != settlement)
        {
            invoice.Settlement = settlement;
            changed = true;
        }

        changed |= AddHistory(invoice, transaction, S);

        return changed;
    }

    // One history entry per payment, take-back and refund on the ledger, found by the ledger entry's id, so it is added
    // once however often the invoice is mirrored.
    private static bool AddHistory(Invoice invoice, Transaction transaction, IStringLocalizer S)
    {
        var known = invoice.History
            .Where(entry => string.Equals(entry.ReferenceType, InvoicingConstants.TransactionEventReferenceType, StringComparison.Ordinal))
            .Select(entry => entry.ReferenceId)
            .ToHashSet(StringComparer.Ordinal);
        var byId = transaction.Events.Where(entry => !string.IsNullOrEmpty(entry.Id)).ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        var currency = transaction.Currency;
        var changed = false;

        foreach (var entry in transaction.Events.OrderBy(entry => entry.CreatedUtc))
        {
            if (string.IsNullOrEmpty(entry.Id) || known.Contains(entry.Id))
            {
                continue;
            }

            var amount = entry.Amount ?? 0m;
            var text = CurrencyScale.Format(amount, currency) + " " + currency;
            string kind = null;
            string message = null;

            switch (entry.Type)
            {
                case TransactionEventType.PaymentRecorded when amount > 0m:
                    kind = DocumentEventKinds.Payment;
                    message = S["Payment of {0} received.", text].Value;
                    break;

                case TransactionEventType.Refunded when amount > 0m:
                    kind = DocumentEventKinds.Refunded;
                    message = S["{0} refunded.", text].Value;
                    break;

                case TransactionEventType.Reversed when !string.IsNullOrEmpty(entry.ReversesEventId) &&
                    byId.TryGetValue(entry.ReversesEventId, out var reversed) &&
                    reversed.Type == TransactionEventType.PaymentRecorded:
                    kind = DocumentEventKinds.Payment;
                    message = string.IsNullOrWhiteSpace(entry.Reason)
                        ? S["The payment of {0} was taken back.", CurrencyScale.Format(reversed.Amount ?? 0m, currency) + " " + currency].Value
                        : S["The payment of {0} was taken back: {1}", CurrencyScale.Format(reversed.Amount ?? 0m, currency) + " " + currency, entry.Reason].Value;
                    amount = -(reversed.Amount ?? 0m);
                    break;
            }

            if (kind is null)
            {
                continue;
            }

            invoice.History.Add(new DocumentEvent
            {
                Id = $"{InvoicingConstants.TransactionEventReferenceType}:{entry.Id}",
                Kind = kind,
                CreatedUtc = entry.CreatedUtc,
                ActorId = entry.ActorId,
                ActorName = entry.ActorName,
                Message = message,
                Amount = amount,
                ReferenceType = InvoicingConstants.TransactionEventReferenceType,
                ReferenceId = entry.Id,
            });

            changed = true;
        }

        return changed;
    }

    private static bool Set(decimal current, decimal value, Action<decimal> set)
    {
        if (current == value)
        {
            return false;
        }

        set(value);

        return true;
    }
}
