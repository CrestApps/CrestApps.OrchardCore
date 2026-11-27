using CrestApps.OrchardCore.Inventory.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Inventory.Drivers;

/// <summary>
/// Builds the editor of a purchase order, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class PurchaseOrderEditDisplayDriver : DisplayDriver<PurchaseOrderEditViewModel>
{
    internal readonly IStringLocalizer S;

    public PurchaseOrderEditDisplayDriver(IStringLocalizer<PurchaseOrderEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(PurchaseOrderEditViewModel model, BuildEditorContext context)
    {
        var results = new List<IDisplayResult>
        {
            Initialize<PurchaseOrderEditViewModel>("PurchaseOrderDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
        };

        results.Add(context.IsNew
            ? Initialize<PurchaseOrderEditViewModel>("PurchaseOrderStart_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Items"], "20"))
            : Initialize<PurchaseOrderEditViewModel>("PurchaseOrderLines_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Items"], "20")));

        return Combine(results.ToArray());
    }

    public override async Task<IDisplayResult> UpdateAsync(PurchaseOrderEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Supplier,
            m => m.SupplierEmail,
            m => m.LocationId,
            m => m.Currency,
            m => m.ExpectedDate,
            m => m.Note);

        if (context.IsNew)
        {
            await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.StartWithLowStock);
        }

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the page's scripts select.
    protected override void BuildPrefix(PurchaseOrderEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(PurchaseOrderEditViewModel source, PurchaseOrderEditViewModel model)
    {
        model.PurchaseOrderId = source.PurchaseOrderId;
        model.Supplier = source.Supplier;
        model.SupplierEmail = source.SupplierEmail;
        model.LocationId = source.LocationId;
        model.Currency = source.Currency;
        model.ExpectedDate = source.ExpectedDate;
        model.Note = source.Note;
        model.StartWithLowStock = source.StartWithLowStock;
        model.Lines = source.Lines;
        model.AddStockItemIds = source.AddStockItemIds;
        model.Order = source.Order;
        model.Locations = source.Locations;
        model.LocationName = source.LocationName;
        model.Currencies = source.Currencies;
        model.Suppliers = source.Suppliers;
        model.CanManage = source.CanManage;
        model.SearchEndpoint = source.SearchEndpoint;
    }
}
