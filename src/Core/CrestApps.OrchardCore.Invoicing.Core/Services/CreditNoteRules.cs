using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The limits of crediting an invoice: a credit note can never credit more than what is left on each invoice line and
/// on the invoice. Only issued credit notes count; drafts and voided ones credit nothing.
/// </summary>
public static class CreditNoteRules
{
    /// <summary>
    /// The code of a credit note that credits nothing.
    /// </summary>
    public const string NothingCredited = "NothingCredited";

    /// <summary>
    /// The code of a credit line that names a line the invoice does not have.
    /// </summary>
    public const string UnknownLine = "UnknownLine";

    /// <summary>
    /// The code of a credit that is more than what is left on an invoice line.
    /// </summary>
    public const string LineExceeded = "LineExceeded";

    /// <summary>
    /// The code of a credit that is more than what is left on the invoice.
    /// </summary>
    public const string TotalExceeded = "TotalExceeded";

    /// <summary>
    /// Returns what is still left to credit on an invoice after its issued credit notes.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="creditNotes">The credit notes of the invoice, in any status.</param>
    /// <returns>What is left per line and in total.</returns>
    public static CreditableAmounts GetCreditable(Invoice invoice, IEnumerable<CreditNote> creditNotes)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var issued = (creditNotes ?? []).Where(note => note?.Status == CreditNoteStatus.Issued).ToList();
        var lines = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var line in invoice.Lines.Where(line => line.HasAmount && !string.IsNullOrEmpty(line.Id)))
        {
            var credited = issued
                .SelectMany(note => note.Lines)
                .Where(creditLine => string.Equals(creditLine.CreditedLineId, line.Id, StringComparison.Ordinal))
                .Sum(creditLine => creditLine.Total);

            lines[line.Id] = Math.Max(0m, line.Total - credited);
        }

        return new CreditableAmounts
        {
            Lines = lines,
            Total = Math.Max(0m, invoice.Totals.Total - issued.Sum(note => note.Totals.Total)),
        };
    }

    /// <summary>
    /// Returns why a credit note cannot be issued against an invoice, or nothing when it can.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="creditNotes">The other credit notes of the invoice, in any status.</param>
    /// <param name="creditNote">The credit note to issue, with its amounts calculated.</param>
    /// <returns>The problems; empty when the credit note is within the limits.</returns>
    public static IReadOnlyList<CreditNoteError> Validate(Invoice invoice, IEnumerable<CreditNote> creditNotes, CreditNote creditNote)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(creditNote);

        var errors = new List<CreditNoteError>();
        var others = (creditNotes ?? []).Where(note => !string.Equals(note?.ItemId, creditNote.ItemId, StringComparison.Ordinal));
        var creditable = GetCreditable(invoice, others);
        var creditLines = creditNote.Lines.Where(line => line.HasAmount).ToList();

        if (creditLines.Count == 0 || creditNote.Totals.Total <= 0m)
        {
            errors.Add(new CreditNoteError(null, NothingCredited, null));

            return errors;
        }

        foreach (var group in creditLines.Where(line => !string.IsNullOrEmpty(line.CreditedLineId)).GroupBy(line => line.CreditedLineId, StringComparer.Ordinal))
        {
            if (!creditable.Lines.TryGetValue(group.Key, out var left))
            {
                errors.Add(new CreditNoteError(group.Key, UnknownLine, null));

                continue;
            }

            if (group.Sum(line => line.Total) > left)
            {
                errors.Add(new CreditNoteError(group.Key, LineExceeded, left));
            }
        }

        if (creditNote.Totals.Total > creditable.Total)
        {
            errors.Add(new CreditNoteError(null, TotalExceeded, creditable.Total));
        }

        return errors;
    }
}
