using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Moves a draft estimate to and from the editor's plain form, with the lines handled as the invoice editor handles them.
/// </summary>
internal static class EstimateDraftEditor
{
    /// <summary>
    /// Fills the editor from a draft, with one blank row to fill in.
    /// </summary>
    public static void Build(Estimate estimate, EstimateEditViewModel model)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentNullException.ThrowIfNull(model);

        model.ItemId = estimate.ItemId;
        model.BillTo = estimate.BillTo?.Party.IsEmpty == false ? estimate.BillTo.Party.ToString() : null;
        model.ContactName = estimate.BillTo?.Contact?.Name;
        model.ContactEmail = estimate.BillTo?.Contact?.Email;
        model.Currency = estimate.Currency;
        model.IssueDate = estimate.IssueDate;
        model.ExpiryDate = estimate.ExpiryDate;
        model.DepositPercent = estimate.DepositPercent;
        model.DepositAmount = estimate.DepositAmount;
        model.OnAcceptance = estimate.OnAcceptance;
        model.PurchaseOrderNumber = estimate.PurchaseOrderNumber;
        model.Reference = estimate.Reference;
        model.PricesIncludeTax = estimate.PricesIncludeTax;
        model.Notes = estimate.Notes;
        model.Terms = estimate.Terms;
        model.Footer = estimate.Footer;
        model.InternalNote = estimate.InternalNote;
        model.Totals = estimate.Totals;
        model.Taxes = estimate.Taxes ?? [];

        model.Lines = InvoiceDraftEditor.BuildLines(estimate.Lines);
        model.Lines.Add(new InvoiceLineEditViewModel { Quantity = 1m });
    }

    /// <summary>
    /// Writes what was posted onto the draft. Totals are worked out after, by the calculator.
    /// </summary>
    public static void Apply(EstimateEditViewModel model, Estimate estimate)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(estimate);

        estimate.BillTo ??= new DocumentParty();
        estimate.BillTo.Party = PartyRef.TryParse(model.BillTo?.Trim(), out var party) ? party : default;
        estimate.BillTo.Contact = (estimate.BillTo.Contact ?? new ContactInfo()) with
        {
            Name = Clean(model.ContactName),
            Email = Clean(model.ContactEmail),
        };

        estimate.Currency = Clean(model.Currency)?.ToUpperInvariant();
        estimate.IssueDate = model.IssueDate;
        estimate.ExpiryDate = model.ExpiryDate;
        estimate.DepositPercent = model.DepositPercent is > 0m ? model.DepositPercent : null;
        estimate.DepositAmount = model.DepositAmount is > 0m ? model.DepositAmount : null;
        estimate.OnAcceptance = model.OnAcceptance;
        estimate.PurchaseOrderNumber = Clean(model.PurchaseOrderNumber);
        estimate.Reference = Clean(model.Reference);
        estimate.PricesIncludeTax = model.PricesIncludeTax;
        estimate.Notes = Clean(model.Notes);
        estimate.Terms = Clean(model.Terms);
        estimate.Footer = Clean(model.Footer);
        estimate.InternalNote = Clean(model.InternalNote);

        InvoiceDraftEditor.ApplyLines(model.Lines, estimate, keepText: false);
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
