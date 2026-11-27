using CrestApps.OrchardCore.Invoicing.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Invoicing.Drivers;

/// <summary>
/// Builds the editor of a draft invoice: one shape per card, so another module can add a card to it.
/// </summary>
internal sealed class InvoiceEditDisplayDriver : DisplayDriver<InvoiceEditViewModel>
{
    internal readonly IStringLocalizer S;

    public InvoiceEditDisplayDriver(IStringLocalizer<InvoiceEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(InvoiceEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<InvoiceEditViewModel>("InvoiceBilledTo_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Billed To"], "10")),
            Initialize<InvoiceEditViewModel>("InvoiceLines_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Lines"], "20")),
            Initialize<InvoiceEditViewModel>("InvoiceSchedule_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Payment Schedule"], "30"))
                .RenderWhen(() => Task.FromResult(model.CanSchedule)),
            Initialize<InvoiceEditViewModel>("InvoicePayment_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Payment"], "40")),
            Initialize<InvoiceEditViewModel>("InvoicePrintedText_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "5").Card(S["Printed Text"], "50")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(InvoiceEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.BillTo, m => m.ContactName, m => m.ContactEmail, m => m.Lines, m => m.Schedule, m => m.Currency, m => m.IssueDate, m => m.PaymentTermId, m => m.PricesIncludeTax, m => m.AllowPartialPayment, m => m.AutoCharge, m => m.LateFeesExempt, m => m.PurchaseOrderNumber, m => m.Reference, m => m.Notes, m => m.Terms, m => m.Footer, m => m.InternalNote);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    /// <inheritdoc/>
    protected override void BuildPrefix(InvoiceEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(InvoiceEditViewModel source, InvoiceEditViewModel target)
    {
        target.ItemId = source.ItemId;
        target.BillTo = source.BillTo;
        target.ContactName = source.ContactName;
        target.ContactEmail = source.ContactEmail;
        target.Currency = source.Currency;
        target.IssueDate = source.IssueDate;
        target.PaymentTermId = source.PaymentTermId;
        target.PurchaseOrderNumber = source.PurchaseOrderNumber;
        target.Reference = source.Reference;
        target.PricesIncludeTax = source.PricesIncludeTax;
        target.AllowPartialPayment = source.AllowPartialPayment;
        target.AutoCharge = source.AutoCharge;
        target.LateFeesExempt = source.LateFeesExempt;
        target.Schedule = source.Schedule;
        target.CanSchedule = source.CanSchedule;
        target.Notes = source.Notes;
        target.Terms = source.Terms;
        target.Footer = source.Footer;
        target.InternalNote = source.InternalNote;
        target.Lines = source.Lines;
        target.BillToName = source.BillToName;
        target.Totals = source.Totals;
        target.Taxes = source.Taxes;
        target.PaymentTerms = source.PaymentTerms;
        target.LineKinds = source.LineKinds;
        target.PartySearchEndpoint = source.PartySearchEndpoint;
        target.CanIssue = source.CanIssue;
    }
}
