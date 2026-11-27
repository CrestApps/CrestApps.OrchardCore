using CrestApps.OrchardCore.Invoicing.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Invoicing.Drivers;

/// <summary>
/// Builds the editor of a draft estimate: one shape per card, so another module can add a card to it.
/// </summary>
internal sealed class EstimateEditDisplayDriver : DisplayDriver<EstimateEditViewModel>
{
    internal readonly IStringLocalizer S;

    public EstimateEditDisplayDriver(IStringLocalizer<EstimateEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(EstimateEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<EstimateEditViewModel>("EstimatePreparedFor_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Prepared For"], "10")),
            Initialize<EstimateEditViewModel>("EstimateLines_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Lines"], "20")),
            Initialize<EstimateEditViewModel>("EstimateOffer_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Offer"], "30")),
            Initialize<EstimateEditViewModel>("EstimatePrintedText_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Printed Text"], "40")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(EstimateEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.BillTo, m => m.ContactName, m => m.ContactEmail, m => m.Lines, m => m.Currency, m => m.ExpiryDate, m => m.DepositPercent, m => m.DepositAmount, m => m.OnAcceptance, m => m.PricesIncludeTax, m => m.PurchaseOrderNumber, m => m.Reference, m => m.Notes, m => m.Terms, m => m.Footer, m => m.InternalNote);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    /// <inheritdoc/>
    protected override void BuildPrefix(EstimateEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(EstimateEditViewModel source, EstimateEditViewModel target)
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
        target.ExpiryDate = source.ExpiryDate;
        target.DepositPercent = source.DepositPercent;
        target.DepositAmount = source.DepositAmount;
        target.OnAcceptance = source.OnAcceptance;
        target.CanSend = source.CanSend;
    }
}
