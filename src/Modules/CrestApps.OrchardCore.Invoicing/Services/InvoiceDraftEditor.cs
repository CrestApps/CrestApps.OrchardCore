using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using OrchardCore;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Moves a draft invoice to and from the editor's plain form: each row keeps its id, a row marked to remove goes, and
/// the blank row the editor offers is kept only when something was typed in it.
/// </summary>
internal static class InvoiceDraftEditor
{
    /// <summary>
    /// Fills the editor from a draft, with one blank row to fill in.
    /// </summary>
    public static void Build(Invoice invoice, InvoiceEditViewModel model)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(model);

        model.ItemId = invoice.ItemId;
        model.BillTo = invoice.BillTo?.Party.IsEmpty == false ? invoice.BillTo.Party.ToString() : null;
        model.ContactName = invoice.BillTo?.Contact?.Name;
        model.ContactEmail = invoice.BillTo?.Contact?.Email;
        model.Currency = invoice.Currency;
        model.IssueDate = invoice.IssueDate;
        model.PaymentTermId = invoice.PaymentTermId;
        model.PurchaseOrderNumber = invoice.PurchaseOrderNumber;
        model.Reference = invoice.Reference;
        model.PricesIncludeTax = invoice.PricesIncludeTax;
        model.AllowPartialPayment = invoice.AllowPartialPayment;
        model.Notes = invoice.Notes;
        model.Terms = invoice.Terms;
        model.Footer = invoice.Footer;
        model.InternalNote = invoice.InternalNote;
        model.Totals = invoice.Totals;
        model.Taxes = invoice.Taxes ?? [];

        model.Lines = BuildLines(invoice.Lines);
        model.Lines.Add(new InvoiceLineEditViewModel { Quantity = 1m });
    }

    /// <summary>
    /// Fills the editor of a draft credit note. It credits the invoice's lines only, so it offers no blank row.
    /// </summary>
    public static void Build(CreditNote creditNote, CreditNoteEditViewModel model)
    {
        ArgumentNullException.ThrowIfNull(creditNote);
        ArgumentNullException.ThrowIfNull(model);

        model.ItemId = creditNote.ItemId;
        model.InvoiceId = creditNote.InvoiceId;
        model.InvoiceNumber = creditNote.InvoiceNumber;
        model.Reason = creditNote.Reason;
        model.Disposition = creditNote.Disposition;
        model.Notes = creditNote.Notes;
        model.Currency = creditNote.Currency;
        model.Totals = creditNote.Totals;
        model.Taxes = creditNote.Taxes ?? [];
        model.Lines = BuildLines(creditNote.Lines);
    }

    /// <summary>
    /// Writes what was posted onto a draft credit note. Totals are worked out after, by the calculator.
    /// </summary>
    public static void Apply(CreditNoteEditViewModel model, CreditNote creditNote)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(creditNote);

        creditNote.Reason = Clean(model.Reason);
        creditNote.Disposition = model.Disposition;
        creditNote.Notes = Clean(model.Notes);

        // Only the lines it already credits: a row the editor did not start from is not added.
        ApplyLines(model.Lines?.Where(row => !string.IsNullOrEmpty(row?.Id)), creditNote, keepText: true);
    }

    /// <summary>
    /// The editor's rows for the lines of an invoice or a credit note.
    /// </summary>
    public static List<InvoiceLineEditViewModel> BuildLines(IEnumerable<DocumentLine> lines)
        => [.. (lines ?? []).Select(line => new InvoiceLineEditViewModel
        {
            Id = line.Id,
            Kind = line.Kind,
            Name = line.Name,
            Description = line.Description,
            Quantity = line.HasAmount ? line.Quantity : null,
            Unit = line.Unit,
            UnitPrice = line.HasAmount ? line.UnitPrice : null,
            DiscountPercent = line.DiscountPercent,
            TaxCategoryCode = line.TaxCategoryCode,
            Total = line.HasAmount ? line.Total : null,
        })];

    /// <summary>
    /// Writes what was posted onto the draft. Totals are not touched: they are worked out after, by the calculator.
    /// </summary>
    public static void Apply(InvoiceEditViewModel model, Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(invoice);

        invoice.BillTo ??= new DocumentParty();
        invoice.BillTo.Party = PartyRef.TryParse(model.BillTo?.Trim(), out var party) ? party : default;
        invoice.BillTo.Contact = (invoice.BillTo.Contact ?? new ContactInfo()) with
        {
            Name = Clean(model.ContactName),
            Email = Clean(model.ContactEmail),
        };

        invoice.Currency = Clean(model.Currency)?.ToUpperInvariant();
        invoice.IssueDate = model.IssueDate;
        invoice.PaymentTermId = Clean(model.PaymentTermId);
        invoice.PurchaseOrderNumber = Clean(model.PurchaseOrderNumber);
        invoice.Reference = Clean(model.Reference);
        invoice.PricesIncludeTax = model.PricesIncludeTax;
        invoice.AllowPartialPayment = model.AllowPartialPayment;
        invoice.Notes = Clean(model.Notes);
        invoice.Terms = Clean(model.Terms);
        invoice.Footer = Clean(model.Footer);
        invoice.InternalNote = Clean(model.InternalNote);

        ApplyLines(model.Lines, invoice, keepText: false);
    }

    /// <summary>
    /// Writes the editor's rows onto the lines of an invoice or a credit note: each row keeps its saved line (with what the
    /// editor doesn't show, such as the line it credits or its manual taxes), a row ticked Remove goes, and a blank row is
    /// ignored.
    /// </summary>
    public static void ApplyLines(IEnumerable<InvoiceLineEditViewModel> rows, InvoicingDocument document, bool keepText)
    {
        ArgumentNullException.ThrowIfNull(document);

        var existing = document.Lines.Where(line => !string.IsNullOrEmpty(line.Id)).ToDictionary(line => line.Id, StringComparer.Ordinal);
        var lines = new List<DocumentLine>();

        foreach (var row in rows ?? [])
        {
            if (row is null || row.Remove || IsBlank(row))
            {
                continue;
            }

            // A row keeps what the editor does not show (its source, service period, manual taxes) from the saved line.
            var line = !string.IsNullOrEmpty(row.Id) && existing.TryGetValue(row.Id, out var saved)
                ? saved
                : new DocumentLine { Id = IdGenerator.GenerateId() };

            // A credit note's line keeps what the line it credits says; only its amounts change.
            if (!keepText || string.IsNullOrEmpty(line.Name))
            {
                line.Kind = row.Kind;
                line.Name = Clean(row.Name);
                line.Description = Clean(row.Description);
                line.Unit = Clean(row.Unit);
                line.TaxCategoryCode = Clean(row.TaxCategoryCode);
            }

            if (line.HasAmount)
            {
                line.Quantity = row.Quantity ?? 1m;
                line.UnitPrice = row.UnitPrice ?? 0m;
                line.DiscountPercent = row.DiscountPercent is > 0m ? row.DiscountPercent : null;
            }
            else
            {
                line.Quantity = 0m;
                line.UnitPrice = 0m;
                line.DiscountPercent = null;
                line.DiscountAmount = null;
            }

            lines.Add(line);
        }

        document.Lines = lines;
    }

    // The blank row offered at the end, left as it was: nothing typed but its default quantity.
    private static bool IsBlank(InvoiceLineEditViewModel row)
        => string.IsNullOrEmpty(row.Id) &&
            string.IsNullOrWhiteSpace(row.Name) &&
            string.IsNullOrWhiteSpace(row.Description) &&
            row.UnitPrice is null or 0m;

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
