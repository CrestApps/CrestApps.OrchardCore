using CrestApps.OrchardCore.Invoicing.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Invoicing.Drivers;

/// <summary>
/// Builds the editor of a recurring plan: one shape per card, so another module can add a card to it.
/// </summary>
internal sealed class RecurringPlanEditDisplayDriver : DisplayDriver<RecurringPlanEditViewModel>
{
    internal readonly IStringLocalizer S;

    public RecurringPlanEditDisplayDriver(IStringLocalizer<RecurringPlanEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(RecurringPlanEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<RecurringPlanEditViewModel>("RecurringPlanSchedule_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Schedule"], "10")),
            Initialize<RecurringPlanEditViewModel>("RecurringPlanBillTo_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Bill To"], "20")),
            Initialize<RecurringPlanEditViewModel>("RecurringPlanLines_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Lines"], "30")),
            Initialize<RecurringPlanEditViewModel>("RecurringPlanUsage_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Usage"], "40"))
                .RenderWhen(() => Task.FromResult(model.Meters is not null)),
            Initialize<RecurringPlanEditViewModel>("RecurringPlanInvoice_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "5").Card(S["Invoice"], "50")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(RecurringPlanEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Frequency, m => m.Interval, m => m.CustomRule, m => m.StartDate, m => m.EndDate, m => m.MaxRuns, m => m.Mode, m => m.PriceIncreasePercent, m => m.PriceIncreaseEveryRuns, m => m.BillUnbilledCharges, m => m.BillTo, m => m.ContactName, m => m.ContactEmail, m => m.Lines, m => m.UsageCharges, m => m.Currency, m => m.PaymentTermId, m => m.PricesIncludeTax, m => m.PurchaseOrderNumber, m => m.Reference, m => m.Notes, m => m.Terms, m => m.InternalNote);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    /// <inheritdoc/>
    protected override void BuildPrefix(RecurringPlanEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(RecurringPlanEditViewModel source, RecurringPlanEditViewModel target)
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
        target.Name = source.Name;
        target.Frequency = source.Frequency;
        target.Interval = source.Interval;
        target.CustomRule = source.CustomRule;
        target.StartDate = source.StartDate;
        target.EndDate = source.EndDate;
        target.MaxRuns = source.MaxRuns;
        target.Mode = source.Mode;
        target.PriceIncreasePercent = source.PriceIncreasePercent;
        target.PriceIncreaseEveryRuns = source.PriceIncreaseEveryRuns;
        target.BillUnbilledCharges = source.BillUnbilledCharges;
        target.UsageCharges = source.UsageCharges;
        target.Meters = source.Meters;
    }
}
