using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Invoicing.Reports;

/// <summary>
/// The sums behind the invoicing reports, kept apart from loading and formatting so they can be checked alone. Every sum
/// is per currency: amounts in different currencies are never added together.
/// </summary>
internal static class InvoicingReportMath
{
    /// <summary>
    /// The aging buckets, by days past due: not due yet, 1 to 30, 31 to 60, 61 to 90 and over 90.
    /// </summary>
    public const int BucketCount = 5;

    /// <summary>
    /// Sums what open invoices still owe per customer and currency, by how long it is past due on <paramref name="today"/>.
    /// An invoice without a due date is due on its issue date.
    /// </summary>
    public static IReadOnlyList<AgingRow> Aging(IEnumerable<Invoice> invoices, DateOnly today)
        => [.. invoices
            .Where(invoice => invoice.Status == InvoiceStatus.Open && invoice.AmountDue > 0m)
            .GroupBy(invoice => (Customer: CustomerName(invoice), Currency: invoice.Currency ?? string.Empty))
            .Select(group =>
            {
                var buckets = new decimal[BucketCount];

                foreach (var invoice in group)
                {
                    buckets[Bucket(invoice, today)] += invoice.AmountDue;
                }

                return new AgingRow(group.Key.Customer, group.Key.Currency, buckets, group.Count());
            })
            .OrderBy(row => row.Currency, StringComparer.Ordinal)
            .ThenByDescending(row => row.Total)];

    /// <summary>
    /// Returns the aging bucket of an open invoice on a day.
    /// </summary>
    public static int Bucket(Invoice invoice, DateOnly today)
    {
        var due = invoice.DueDate ?? invoice.IssueDate ?? today;
        var late = today.DayNumber - due.DayNumber;

        return late switch
        {
            <= 0 => 0,
            <= 30 => 1,
            <= 60 => 2,
            <= 90 => 3,
            _ => 4,
        };
    }

    /// <summary>
    /// Sums what was invoiced per customer and currency, less what credit notes issued in the same period took off.
    /// Voided invoices and credit notes count for nothing.
    /// </summary>
    public static IReadOnlyList<SalesRow> SalesByCustomer(IEnumerable<Invoice> invoices, IEnumerable<CreditNote> creditNotes)
    {
        var rows = new Dictionary<(string Key, string Currency), SalesRow>();

        foreach (var invoice in invoices.Where(invoice => invoice.Status is InvoiceStatus.Open or InvoiceStatus.Uncollectible))
        {
            var key = (CustomerKey(invoice), invoice.Currency ?? string.Empty);
            var row = rows.TryGetValue(key, out var found) ? found : new SalesRow(CustomerName(invoice), key.Item2);

            rows[key] = row with
            {
                Documents = row.Documents + 1,
                Net = row.Net + Net(invoice),
                Tax = row.Tax + (invoice.Totals?.TaxTotal ?? 0m),
                Total = row.Total + (invoice.Totals?.Total ?? 0m),
            };
        }

        foreach (var creditNote in creditNotes.Where(note => note.Status == CreditNoteStatus.Issued))
        {
            var key = (CustomerKey(creditNote), creditNote.Currency ?? string.Empty);
            var row = rows.TryGetValue(key, out var found) ? found : new SalesRow(CustomerName(creditNote), key.Item2);

            rows[key] = row with
            {
                Credited = row.Credited + (creditNote.Totals?.Total ?? 0m),
                Net = row.Net - Net(creditNote),
                Tax = row.Tax - (creditNote.Totals?.TaxTotal ?? 0m),
                Total = row.Total - (creditNote.Totals?.Total ?? 0m),
            };
        }

        return [.. rows.Values.OrderBy(row => row.Currency, StringComparer.Ordinal).ThenByDescending(row => row.Total)];
    }

    /// <summary>
    /// Sums the lines invoiced per item (product, SKU or name) and currency: how many and for how much before tax, after
    /// the invoice's discounts, less the lines of credit notes issued in the same period. Voided invoices, and text and
    /// section lines, count for nothing.
    /// </summary>
    public static IReadOnlyList<ItemSalesRow> SalesByItem(IEnumerable<Invoice> invoices, IEnumerable<CreditNote> creditNotes)
    {
        static IEnumerable<(InvoicingDocument Document, DocumentLine Line, int Sign)> Lines(InvoicingDocument document, int sign)
            => document.Lines.Where(line => line.Kind is not (DocumentLineKind.Text or DocumentLineKind.Section)).Select(line => (document, line, sign));

        var entries = invoices
            .Where(invoice => invoice.Status is InvoiceStatus.Open or InvoiceStatus.Uncollectible)
            .SelectMany(invoice => Lines(invoice, 1))
            .Concat(creditNotes.Where(note => note.Status == CreditNoteStatus.Issued).SelectMany(note => Lines(note, -1)));

        return [.. entries
            .GroupBy(entry => (Key: ItemKey(entry.Line), Currency: entry.Document.Currency ?? string.Empty))
            .Select(group =>
            {
                var first = group.First(entry => entry.Sign > 0 || group.All(other => other.Sign < 0)).Line;

                return new ItemSalesRow(
                    first.Name ?? first.Sku ?? string.Empty,
                    first.Sku,
                    group.Key.Currency,
                    group.Sum(entry => entry.Sign * entry.Line.Quantity),
                    group.Sum(entry => entry.Sign * (entry.Line.Net - entry.Line.DocumentDiscount)),
                    group.Where(entry => entry.Sign > 0).Select(entry => entry.Document.ItemId).Distinct(StringComparer.Ordinal).Count());
            })
            .OrderBy(row => row.Currency, StringComparer.Ordinal)
            .ThenByDescending(row => row.Net)];
    }

    /// <summary>
    /// Sums the taxes per tax, rate and currency: what was taxed and the tax, on invoices issued less credit notes issued.
    /// Voided invoices count for nothing.
    /// </summary>
    public static IReadOnlyList<TaxRow> TaxSummary(IEnumerable<Invoice> invoices, IEnumerable<CreditNote> creditNotes)
    {
        var rows = new Dictionary<(string Name, decimal Rate, string Currency), TaxRow>();

        void Add(InvoicingDocument document, int sign)
        {
            foreach (var tax in document.Taxes ?? [])
            {
                var name = tax.Name ?? tax.Code ?? string.Empty;
                var key = (name, tax.Rate, document.Currency ?? string.Empty);
                var row = rows.TryGetValue(key, out var found) ? found : new TaxRow(name, tax.Rate, key.Item3);

                rows[key] = row with
                {
                    Taxable = row.Taxable + (sign * tax.TaxableAmount),
                    Tax = row.Tax + (sign * tax.TaxAmount),
                    CreditedTax = sign < 0 ? row.CreditedTax + tax.TaxAmount : row.CreditedTax,
                };
            }
        }

        foreach (var invoice in invoices.Where(invoice => invoice.Status is InvoiceStatus.Open or InvoiceStatus.Uncollectible))
        {
            Add(invoice, 1);
        }

        foreach (var creditNote in creditNotes.Where(note => note.Status == CreditNoteStatus.Issued))
        {
            Add(creditNote, -1);
        }

        return [.. rows.Values.OrderBy(row => row.Currency, StringComparer.Ordinal).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Rate)];
    }

    /// <summary>
    /// Lists the payments recorded on invoices' transactions in a period, each less what was taken back of it in the
    /// period, with the refunds given in the period.
    /// </summary>
    public static IReadOnlyList<PaymentRow> Payments(IEnumerable<Transaction> transactions, DateTime fromUtc, DateTime toUtc)
    {
        var rows = new List<PaymentRow>();

        foreach (var transaction in transactions)
        {
            var reversed = transaction.Events
                .Where(entry => entry.Type == TransactionEventType.Reversed && !string.IsNullOrEmpty(entry.ReversesEventId))
                .Select(entry => entry.ReversesEventId)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var entry in transaction.Events.Where(entry => entry.CreatedUtc >= fromUtc && entry.CreatedUtc < toUtc && entry.Amount is > 0m))
            {
                if (entry.Type == TransactionEventType.PaymentRecorded && !reversed.Contains(entry.Id))
                {
                    rows.Add(new PaymentRow(entry.CreatedUtc, transaction.InvoiceNumber, transaction.PayerContact?.Name ?? transaction.GuestContactName, entry.PaymentMethod, entry.Method, entry.Reference, transaction.Currency, entry.Amount.Value, IsRefund: false));
                }
                else if (entry.Type == TransactionEventType.Refunded)
                {
                    rows.Add(new PaymentRow(entry.CreatedUtc, transaction.InvoiceNumber, transaction.PayerContact?.Name ?? transaction.GuestContactName, entry.PaymentMethod, entry.Method, entry.Reference, transaction.Currency, entry.Amount.Value, IsRefund: true));
                }
            }
        }

        return [.. rows.OrderBy(row => row.WhenUtc)];
    }

    private static decimal Net(InvoicingDocument document)
        => (document.Totals?.Total ?? 0m) - (document.Totals?.TaxTotal ?? 0m);

    private static string CustomerKey(InvoicingDocument document)
        => document.BillTo?.Party is { IsEmpty: false } party ? party.ToString() : "name:" + CustomerName(document);

    private static string CustomerName(InvoicingDocument document)
        => document.BillTo?.Contact?.Name ?? document.BillTo?.Contact?.Email ?? string.Empty;

    private static string ItemKey(DocumentLine line)
        => !string.IsNullOrEmpty(line.ProductId) ? "product:" + line.ProductId
            : !string.IsNullOrEmpty(line.Sku) ? "sku:" + line.Sku
            : "name:" + (line.Name ?? string.Empty).Trim().ToUpperInvariant();
}

/// <summary>
/// What one customer owes in one currency, by aging bucket.
/// </summary>
internal sealed record AgingRow(string Customer, string Currency, decimal[] Buckets, int Invoices)
{
    public decimal Total => Buckets.Sum();
}

/// <summary>
/// What was invoiced to one customer in one currency.
/// </summary>
internal sealed record SalesRow(string Customer, string Currency, int Documents = 0, decimal Net = 0m, decimal Tax = 0m, decimal Total = 0m, decimal Credited = 0m);

/// <summary>
/// What was invoiced of one item in one currency.
/// </summary>
internal sealed record ItemSalesRow(string Name, string Sku, string Currency, decimal Quantity, decimal Net, int Invoices);

/// <summary>
/// One tax at one rate in one currency.
/// </summary>
internal sealed record TaxRow(string Name, decimal Rate, string Currency, decimal Taxable = 0m, decimal Tax = 0m, decimal CreditedTax = 0m);

/// <summary>
/// One payment or refund on an invoice's transaction.
/// </summary>
internal sealed record PaymentRow(DateTime WhenUtc, string InvoiceNumber, string Customer, string PaymentMethod, string SettlementMethod, string Reference, string Currency, decimal Amount, bool IsRefund);
