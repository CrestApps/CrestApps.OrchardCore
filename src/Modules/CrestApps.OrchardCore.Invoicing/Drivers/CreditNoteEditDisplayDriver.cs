using CrestApps.OrchardCore.Invoicing.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Invoicing.Drivers;

/// <summary>
/// Builds the editor of a draft credit note: one shape per card, so another module can add a card to it.
/// </summary>
internal sealed class CreditNoteEditDisplayDriver : DisplayDriver<CreditNoteEditViewModel>
{
    internal readonly IStringLocalizer S;

    public CreditNoteEditDisplayDriver(IStringLocalizer<CreditNoteEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(CreditNoteEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<CreditNoteEditViewModel>("CreditNoteCredit_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Credit"], "10")),
            Initialize<CreditNoteEditViewModel>("CreditNoteLines_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Lines"], "20")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(CreditNoteEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Reason, m => m.Disposition, m => m.Notes, m => m.Lines);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    /// <inheritdoc/>
    protected override void BuildPrefix(CreditNoteEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(CreditNoteEditViewModel source, CreditNoteEditViewModel target)
    {
        target.ItemId = source.ItemId;
        target.Reason = source.Reason;
        target.Disposition = source.Disposition;
        target.Notes = source.Notes;
        target.Lines = source.Lines;
        target.InvoiceId = source.InvoiceId;
        target.InvoiceNumber = source.InvoiceNumber;
        target.Currency = source.Currency;
        target.Totals = source.Totals;
        target.Taxes = source.Taxes;
        target.Creditable = source.Creditable;
        target.Dispositions = source.Dispositions;
        target.CanIssue = source.CanIssue;
    }
}
