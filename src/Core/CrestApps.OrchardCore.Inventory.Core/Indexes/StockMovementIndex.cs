using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Inventory.Core.Indexes;

public sealed class StockMovementIndex : CatalogItemIndex
{
    public string StockItemId { get; set; }

    public StockMovementKind Kind { get; set; }

    public string OrderId { get; set; }

    public DateTime Utc { get; set; }
}

public sealed class StockMovementIndexProvider : IndexProvider<StockMovement>
{
    public StockMovementIndexProvider()
    {
        CollectionName = InventoryConstants.CollectionName;
    }

    public override void Describe(DescribeContext<StockMovement> context)
    {
        context.For<StockMovementIndex>()
            .Map(movement => new StockMovementIndex
            {
                ItemId = movement.ItemId,
                StockItemId = movement.StockItemId,
                Kind = movement.Kind,
                OrderId = movement.OrderId,
                Utc = movement.Utc,
            });
    }
}
