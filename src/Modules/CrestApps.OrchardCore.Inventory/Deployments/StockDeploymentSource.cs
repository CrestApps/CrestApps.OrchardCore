using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Inventory.Core.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Inventory.Deployments;

internal sealed class StockDeploymentSource : DeploymentSourceBase<StockDeploymentStep>
{
    private const int PageSize = 500;

    private readonly IStockItemStore _items;

    public StockDeploymentSource(IStockItemStore items)
    {
        _items = items;
    }

    protected override async Task ProcessAsync(StockDeploymentStep step, DeploymentPlanResult result)
    {
        var items = new JsonArray();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = await _items.ListForExportAsync(skip, PageSize);

            foreach (var item in page)
            {
                // Both the ids and the SKU: the ids match on a copy of this site, the SKU on any other.
                items.Add(new JsonObject
                {
                    ["ContentItemId"] = item.ContentItemId,
                    ["VariantId"] = item.VariantId,
                    ["Sku"] = item.Sku,
                    ["OnHand"] = item.OnHand,
                });
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Items"] = items,
        });
    }
}
