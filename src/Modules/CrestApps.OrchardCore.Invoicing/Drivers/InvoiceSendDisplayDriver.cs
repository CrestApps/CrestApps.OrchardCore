using CrestApps.OrchardCore.Invoicing.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Invoicing.Drivers;

/// <summary>
/// Builds the editor of the dialog that sends an invoice: one shape per card, so another module can add a card to it.
/// </summary>
internal sealed class InvoiceSendDisplayDriver : DisplayDriver<InvoiceSendViewModel>
{
    internal readonly IStringLocalizer S;

    public InvoiceSendDisplayDriver(IStringLocalizer<InvoiceSendDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(InvoiceSendViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<InvoiceSendViewModel>("InvoiceSendRecipients_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Recipients"], "10")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(InvoiceSendViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.To, m => m.Cc, m => m.Bcc);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    /// <inheritdoc/>
    protected override void BuildPrefix(InvoiceSendViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(InvoiceSendViewModel source, InvoiceSendViewModel target)
    {
        target.ItemId = source.ItemId;
        target.To = source.To;
        target.Cc = source.Cc;
        target.Bcc = source.Bcc;
        target.Number = source.Number;
        target.IssuesFirst = source.IssuesFirst;
        target.CanSend = source.CanSend;
    }
}
