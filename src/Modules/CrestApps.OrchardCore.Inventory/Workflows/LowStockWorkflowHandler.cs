using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using OrchardCore.ContentManagement;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Inventory.Workflows;

/// <summary>
/// Starts the <see cref="LowStockEvent"/> workflows when a product runs low or sells out.
/// </summary>
internal sealed class LowStockWorkflowHandler : IStockLevelHandler
{
    private readonly IWorkflowManager _workflowManager;
    private readonly IContentManager _contentManager;

    public LowStockWorkflowHandler(IWorkflowManager workflowManager, IContentManager contentManager)
    {
        _workflowManager = workflowManager;
        _contentManager = contentManager;
    }

    public async Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
    {
        if (!context.RanLow)
        {
            return;
        }

        var item = context.Item;
        var input = new Dictionary<string, object>
        {
            ["ContentItem"] = await _contentManager.GetAsync(item.ContentItemId, VersionOptions.Latest),
            ["StockItem"] = item,
            ["StockItemId"] = item.ItemId,
            ["ContentItemId"] = item.ContentItemId,
            ["VariantId"] = item.VariantId,
            ["Sku"] = item.Sku,
            ["Title"] = item.Title,
            ["Available"] = item.Available,
            ["LowStockThreshold"] = item.LowStockThreshold,
            ["SoldOut"] = context.Level == StockLevel.Out,
        };

        await _workflowManager.TriggerEventAsync(nameof(LowStockEvent), input, correlationId: item.ItemId);
    }
}
