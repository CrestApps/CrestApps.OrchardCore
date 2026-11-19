using CrestApps.Core.AI;
using CrestApps.OrchardCore.Inventory.AI;
using CrestApps.OrchardCore.Inventory.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Inventory;

/// <summary>
/// Registers the inventory AI tools whenever the Inventory and AI features are on. Reading needs <c>ViewInventory</c>
/// and changing needs <c>ManageInventory</c>, as on Commerce › Inventory; stock changes are recorded as movements with a
/// reason, and removing a stock location asks for approval.
/// </summary>
[Feature(InventoryConstants.Features.Inventory)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<InventoryToolScope>();

        services.AddCoreAITool<ListStockItemsTool>(ListStockItemsTool.TheName)
            .WithTitle(S["List Stock Items"])
            .WithDescription(S["Lists the products and variants the store counts, with SKU, units on hand, held and available, the low-stock threshold and whether backorders are allowed. Filters by text (SKU or title) and by all, low, out of stock or backordered, with paging; use it to answer stock questions and to get a stock item id."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<GetStockItemTool>(GetStockItemTool.TheName)
            .WithTitle(S["Get Stock Item"])
            .WithDescription(S["Returns one stock item, found by id, by SKU, or by product and variant, with its level at each location, the units held for unpaid orders, how many shoppers wait for a back-in-stock email, and a page of its movement history. Use it before changing stock."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<AdjustStockTool>(AdjustStockTool.TheName)
            .WithTitle(S["Adjust Stock"])
            .WithDescription(S["Changes a stock item's units at one location: receive adds units that arrived, adjust corrects up or down, count sets the units on hand to what was counted. Each change is recorded as a movement with its reason; if someone else changed the same item at the same moment it reports busy and nothing is saved."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<TransferStockTool>(TransferStockTool.TheName)
            .WithTitle(S["Transfer Stock"])
            .WithDescription(S["Moves units of a stock item from one location to another without changing the total on hand. Only units that are available (not held for orders) can move, and the destination must be a listed location."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<ImportStockCountsTool>(ImportStockCountsTool.TheName)
            .WithTitle(S["Import Stock Counts"])
            .WithDescription(S["Sets the units on hand of many items at once from rows of SKU (or content item id and variant id) and counted units. It is all or nothing: if any row is wrong nothing changes and the problems come back by row; at most 10,000 rows."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<RebuildStockItemTool>(RebuildStockItemTool.TheName)
            .WithTitle(S["Rebuild Stock Item"])
            .WithDescription(S["Recomputes one stock item's units on hand from its movement history. Use it only when the stored count is in doubt."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<SyncInventoryTool>(SyncInventoryTool.TheName)
            .WithTitle(S["Sync Inventory"])
            .WithDescription(S["Starts counting products whose type gained the Inventory part and refreshes every stock item's title, SKU and threshold from its product; returns how many products were checked. Use it after adding the Inventory part to a type or when stock items look stale."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<ListStockLocationsTool>(ListStockLocationsTool.TheName)
            .WithTitle(S["List Stock Locations"])
            .WithDescription(S["Lists the store's stock locations in the order orders take units from them, with id, name and address. Use it to get a location id for stock changes, purchase orders, returns and registers."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<SaveStockLocationTool>(SaveStockLocationTool.TheName)
            .WithTitle(S["Save Stock Location"])
            .WithDescription(S["Adds a stock location or changes its name, address or place in the order orders take units from. A location keeps its id because stock levels and movements point at it."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<DeleteStockLocationTool>(DeleteStockLocationTool.TheName)
            .WithTitle(S["Delete Stock Location"])
            .WithDescription(S["Removes a stock location from the list. The default location cannot be removed; units still at a removed location stay on their items and can be sold or transferred out."])
            .WithCategory(S["Inventory"])
            .RequireApproval()
            .Selectable();
    }
}
