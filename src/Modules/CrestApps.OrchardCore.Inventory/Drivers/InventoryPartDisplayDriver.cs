using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Inventory.Drivers;

/// <summary>
/// How a product's stock is kept, and what is on hand now. The counts are changed under Commerce > Inventory, where
/// every change is recorded.
/// </summary>
public sealed class InventoryPartDisplayDriver : ContentPartDisplayDriver<InventoryPart>
{
    private readonly IStockItemStore _items;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public InventoryPartDisplayDriver(IStockItemStore items, ISiteService siteService, IStringLocalizer<InventoryPartDisplayDriver> stringLocalizer)
    {
        _items = items;
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(InventoryPart part, BuildPartEditorContext context)
        => Initialize<InventoryPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.TrackInventory = part.TrackInventory;
            model.AllowBackorder = part.AllowBackorder;
            model.LowStockThreshold = part.LowStockThreshold;
            model.ReleaseDate = part.ReleaseDateUtc?.Date;
            model.DefaultLowStockThreshold = (await _siteService.GetSettingsAsync<InventorySettings>()).DefaultLowStockThreshold;
            model.IsNew = context.IsNew;

            if (!context.IsNew)
            {
                model.StockItems = (await _items.ListByProductAsync(part.ContentItem.ContentItemId))
                    .Where(item => !item.Archived)
                    .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        });

    public override async Task<IDisplayResult> UpdateAsync(InventoryPart part, UpdatePartEditorContext context)
    {
        var model = new InventoryPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.LowStockThreshold is < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.LowStockThreshold), S["The low-stock threshold can't be negative."]);
        }

        part.TrackInventory = model.TrackInventory;
        part.AllowBackorder = model.AllowBackorder;
        part.LowStockThreshold = model.LowStockThreshold;

        // A day, kept as noon UTC on it, so it reads as the same day in any time zone the store or its buyers are in.
        part.ReleaseDateUtc = model.ReleaseDate is { } date ? DateTime.SpecifyKind(date.Date.AddHours(12), DateTimeKind.Utc) : null;

        return Edit(part, context);
    }
}
