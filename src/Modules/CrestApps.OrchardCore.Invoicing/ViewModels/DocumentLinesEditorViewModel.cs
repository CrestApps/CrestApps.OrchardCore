using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The line items editor shared by invoices, estimates, recurring plans and credit notes.
/// </summary>
public sealed class DocumentLinesEditorViewModel
{
    public List<InvoiceLineEditViewModel> Lines { get; init; } = [];

    public IReadOnlyList<SelectListItem> LineKinds { get; init; } = [];

    public string Currency { get; init; }

    public bool PricesIncludeTax { get; init; }

    public DocumentTotals Totals { get; init; }

    public IList<DocumentTax> Taxes { get; init; } = [];

    /// <summary>
    /// Gets whether lines can be added, moved and picked from products; a credit note only credits the invoice's lines.
    /// </summary>
    public bool CanAddLines { get; init; }

    /// <summary>
    /// Gets whether a line's kind, description, unit and tax category can be changed; a credit note's lines keep the
    /// invoice's.
    /// </summary>
    public bool CanEditText { get; init; }

    public string Hint { get; init; }

    public string TotalLabel { get; init; }

    /// <summary>
    /// The editor of a draft invoice, estimate or recurring plan.
    /// </summary>
    public static DocumentLinesEditorViewModel For(InvoiceEditViewModel model, string hint, string totalLabel)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new DocumentLinesEditorViewModel
        {
            Lines = model.Lines,
            LineKinds = model.LineKinds,
            Currency = model.Currency,
            PricesIncludeTax = model.PricesIncludeTax,
            Totals = model.Totals,
            Taxes = model.Taxes ?? [],
            CanAddLines = true,
            CanEditText = true,
            Hint = hint,
            TotalLabel = totalLabel,
        };
    }

    /// <summary>
    /// The editor of a draft credit note: the invoice's lines, whose quantity, price and discount can be lowered or which
    /// can be left out.
    /// </summary>
    public static DocumentLinesEditorViewModel For(CreditNoteEditViewModel model, string hint, string totalLabel)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new DocumentLinesEditorViewModel
        {
            Lines = model.Lines,
            Currency = model.Currency,
            Totals = model.Totals,
            Taxes = model.Taxes ?? [],
            CanAddLines = false,
            CanEditText = false,
            Hint = hint,
            TotalLabel = totalLabel,
        };
    }
}

/// <summary>
/// One row of the line items editor: a line, or the empty row the editor adds lines from (its index <c>__index__</c>).
/// </summary>
public sealed class DocumentLineRowViewModel
{
    public InvoiceLineEditViewModel Line { get; init; }

    public string Index { get; init; }

    public int? Position { get; init; }

    public DocumentLinesEditorViewModel Editor { get; init; }

    public IReadOnlyList<SelectListItem> TaxCategories { get; init; } = [];

    public bool CanPickProduct { get; init; }
}
