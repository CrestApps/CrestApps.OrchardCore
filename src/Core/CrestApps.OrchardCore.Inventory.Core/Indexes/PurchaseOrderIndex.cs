using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Inventory.Core.Indexes;

public sealed class PurchaseOrderIndex : CatalogItemIndex
{
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the supplier's name in upper case, for finding orders by supplier.
    /// </summary>
    public string NormalizedSupplier { get; set; }

    public PurchaseOrderStatus Status { get; set; }

    public string LocationId { get; set; }

    public DateTime? ExpectedUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class PurchaseOrderIndexProvider : IndexProvider<PurchaseOrder>
{
    public PurchaseOrderIndexProvider()
    {
        CollectionName = InventoryConstants.CollectionName;
    }

    public override void Describe(DescribeContext<PurchaseOrder> context)
    {
        context.For<PurchaseOrderIndex>()
            .Map(order => new PurchaseOrderIndex
            {
                ItemId = order.ItemId,
                Number = order.Number,
                NormalizedSupplier = order.Supplier?.Trim().ToUpperInvariant(),
                Status = order.Status,
                LocationId = order.LocationId,
                ExpectedUtc = order.ExpectedUtc,
                CreatedUtc = order.CreatedUtc,
            });
    }
}
