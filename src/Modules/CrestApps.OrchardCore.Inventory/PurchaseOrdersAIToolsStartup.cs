using CrestApps.Core.AI;
using CrestApps.OrchardCore.Inventory.AI;
using CrestApps.OrchardCore.Inventory.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Inventory;

/// <summary>
/// Registers the purchase order AI tools whenever the Purchase Orders and AI features are on. Reading needs
/// <c>ViewInventory</c> and changing needs <c>ManageInventory</c>, as on Commerce › Purchase orders; canceling and
/// deleting a purchase order ask for approval. No tool sends anything to a supplier.
/// </summary>
[Feature(InventoryConstants.Features.PurchaseOrders)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class PurchaseOrdersAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PurchaseOrdersAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PurchaseOrdersAIToolsStartup(IStringLocalizer<PurchaseOrdersAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<InventoryToolScope>();

        services.AddCoreAITool<ListPurchaseOrdersTool>(ListPurchaseOrdersTool.TheName)
            .WithTitle(S["List Purchase Orders"])
            .WithDescription(S["Lists purchase orders with number, supplier, status, location, expected date, units ordered and received, and total; filters by text and status, with paging."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<GetPurchaseOrderTool>(GetPurchaseOrderTool.TheName)
            .WithTitle(S["Get Purchase Order"])
            .WithDescription(S["Returns one purchase order with its details and lines (stock item, SKU, ordered, received, outstanding, unit cost). Use it before changing or receiving it."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<SavePurchaseOrderTool>(SavePurchaseOrderTool.TheName)
            .WithTitle(S["Save Purchase Order"])
            .WithDescription(S["Creates a draft purchase order (supplier, location, expected date, note, lines, or started from the items that are low on stock) or changes one that is not yet received or canceled. It does not send anything to the supplier."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<MarkPurchaseOrderOrderedTool>(MarkPurchaseOrderOrderedTool.TheName)
            .WithTitle(S["Mark Purchase Order Ordered"])
            .WithDescription(S["Marks a draft purchase order with at least one line as ordered, so its items can be received when they arrive."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<ReceivePurchaseOrderTool>(ReceivePurchaseOrderTool.TheName)
            .WithTitle(S["Receive Purchase Order"])
            .WithDescription(S["Records items of an ordered purchase order as arrived, by line and quantity or everything still outstanding, adding them to stock at the order's location. The order becomes received once nothing is outstanding."])
            .WithCategory(S["Inventory"])
            .Selectable();

        services.AddCoreAITool<CancelPurchaseOrderTool>(CancelPurchaseOrderTool.TheName)
            .WithTitle(S["Cancel Purchase Order"])
            .WithDescription(S["Cancels a purchase order that is not fully received. What already arrived stays in stock; a canceled order cannot be changed or reopened."])
            .WithCategory(S["Inventory"])
            .AsDestructive()
            .Selectable();

        services.AddCoreAITool<DeletePurchaseOrderTool>(DeletePurchaseOrderTool.TheName)
            .WithTitle(S["Delete Purchase Order"])
            .WithDescription(S["Deletes a draft purchase order. An order already sent to a supplier cannot be deleted, only canceled."])
            .WithCategory(S["Inventory"])
            .AsDestructive()
            .Selectable();
    }
}
