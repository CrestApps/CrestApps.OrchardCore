using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Shipping.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Shipping.Indexes;

public sealed class ShipmentIndex : CatalogItemIndex
{
    public string OrderId { get; set; }

    public ShipmentStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? ShippedUtc { get; set; }

    public string TrackingNumber { get; set; }
}

public sealed class ShipmentIndexProvider : IndexProvider<Shipment>
{
    public ShipmentIndexProvider()
    {
        CollectionName = ShippingConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Shipment> context)
    {
        context.For<ShipmentIndex>()
            .Map(shipment => new ShipmentIndex
            {
                ItemId = shipment.ItemId,
                OrderId = shipment.OrderId,
                Status = shipment.Status,
                CreatedUtc = shipment.CreatedUtc,
                ShippedUtc = shipment.ShippedUtc,
                TrackingNumber = string.IsNullOrWhiteSpace(shipment.TrackingNumber) ? null : shipment.TrackingNumber.Trim(),
            });
    }
}
