using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Inventory.Core.Indexes;

public sealed class StockReservationIndex : CatalogItemIndex
{
    public string StockItemId { get; set; }

    public string OrderId { get; set; }

    public StockReservationStatus Status { get; set; }

    public DateTime ExpiresUtc { get; set; }
}

public sealed class StockReservationIndexProvider : IndexProvider<StockReservation>
{
    public StockReservationIndexProvider()
    {
        CollectionName = InventoryConstants.CollectionName;
    }

    public override void Describe(DescribeContext<StockReservation> context)
    {
        context.For<StockReservationIndex>()
            .Map(reservation => new StockReservationIndex
            {
                ItemId = reservation.ItemId,
                StockItemId = reservation.StockItemId,
                OrderId = reservation.OrderId,
                Status = reservation.Status,
                ExpiresUtc = reservation.ExpiresUtc,
            });
    }
}
