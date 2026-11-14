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

        model.Lines = [.. invoice.Lines.Select(line => new InvoiceLineEditViewModel
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

        model.Lines.Add(new InvoiceLineEditViewModel { Quantity = 1m });
    }

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
        invoice.PaymentTermId = Clean(model.PaymentTermId);
        invoice.PurchaseOrderNumber = Clean(model.PurchaseOrderNumber);
        invoice.Reference = Clean(model.Reference);
        invoice.PricesIncludeTax = model.PricesIncludeTax;
        invoice.AllowPartialPayment = model.AllowPartialPayment;
        invoice.Notes = Clean(model.Notes);
        invoice.Terms = Clean(model.Terms);
        invoice.Footer = Clean(model.Footer);
        invoice.InternalNote = Clean(model.InternalNote);

        var existing = invoice.Lines.Where(line => !string.IsNullOrEmpty(line.Id)).ToDictionary(line => line.Id, StringComparer.Ordinal);
        var lines = new List<DocumentLine>();

        foreach (var row in model.Lines ?? [])
        {
            if (row is null || row.Remove || IsBlank(row))
            {
                continue;
            }

            // A row keeps what the editor does not show (its source, service period, manual taxes) from the saved line.
            var line = !string.IsNullOrEmpty(row.Id) && existing.TryGetValue(row.Id, out var saved)
                ? saved
                : new DocumentLine { Id = IdGenerator.GenerateId() };

            line.Kind = row.Kind;
            line.Name = Clean(row.Name);
            line.Description = Clean(row.Description);
            line.Unit = Clean(row.Unit);
            line.TaxCategoryCode = Clean(row.TaxCategoryCode);

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

        invoice.Lines = lines;
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
