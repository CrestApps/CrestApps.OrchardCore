using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Builds the editor of a draft order: one shape per card.
/// </summary>
internal sealed class DraftOrderEditDisplayDriver : DisplayDriver<DraftOrderEditViewModel>
{
    internal readonly IStringLocalizer S;

    public DraftOrderEditDisplayDriver(IStringLocalizer<DraftOrderEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(DraftOrderEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<DraftOrderEditViewModel>("DraftOrderBuyer_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Buyer"], "10")),
            Initialize<DraftOrderEditViewModel>("DraftOrderItems_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Items"], "20")),
            Initialize<DraftOrderEditViewModel>("DraftOrderAddProducts_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Items"], "20")),
            Initialize<DraftOrderEditViewModel>("DraftOrderInvoice_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Invoice"], "30")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(DraftOrderEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.CustomerEmail, m => m.CustomerName, m => m.Lines, m => m.AddProductIds, m => m.Note, m => m.DueDate);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    /// <inheritdoc/>
    protected override void BuildPrefix(DraftOrderEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(DraftOrderEditViewModel source, DraftOrderEditViewModel target)
    {
        target.CustomerEmail = source.CustomerEmail;
        target.CustomerName = source.CustomerName;
        target.Note = source.Note;
        target.DueDate = source.DueDate;
        target.AddsToOrderId = source.AddsToOrderId;
        target.Lines = source.Lines;
        target.AddProductIds = source.AddProductIds;
        target.Order = source.Order;
        target.AddsToOrderNumber = source.AddsToOrderNumber;
        target.Problems = source.Problems;
        target.Subtotal = source.Subtotal;
        target.Currency = source.Currency;
        target.ProductSearchEndpoint = source.ProductSearchEndpoint;
        target.CanEmail = source.CanEmail;
    }
}
