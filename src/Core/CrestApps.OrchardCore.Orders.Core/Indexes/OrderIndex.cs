using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Orders.Core.Indexes;

/// <summary>
/// Finds orders by number, customer, status, cart and checkout.
/// </summary>
public sealed class OrderIndex : CatalogItemIndex
{
    public string OrderNumber { get; set; }

    public string UserId { get; set; }

    public string CustomerId { get; set; }

    public string CustomerEmail { get; set; }

    public string CartId { get; set; }

    public string CheckoutSessionId { get; set; }

    public OrderStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? AnonymizedUtc { get; set; }

    public string Channel { get; set; }

    public string LocationId { get; set; }

    public string ShiftId { get; set; }

    public string CompanyId { get; set; }

    public string PurchaseOrderNumber { get; set; }
}

public sealed class OrderIndexProvider : IndexProvider<Order>
{
    public OrderIndexProvider()
    {
        CollectionName = OrderConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Order> context)
    {
        context.For<OrderIndex>()
            .Map(order => new OrderIndex
            {
                ItemId = order.ItemId,
                OrderNumber = order.OrderNumber,
                UserId = order.UserId,
                CustomerId = order.CustomerId,
                CustomerEmail = order.CustomerEmail?.Trim().ToUpperInvariant(),
                AnonymizedUtc = order.AnonymizedUtc,
                CartId = order.CartId,
                CheckoutSessionId = order.CheckoutSessionId,
                Status = order.Status,
                CreatedUtc = order.CreatedUtc,
                Channel = OrderChannels.Of(order),
                LocationId = order.LocationId,
                ShiftId = order.ShiftId,
                CompanyId = order.CompanyId,
                PurchaseOrderNumber = string.IsNullOrWhiteSpace(order.PurchaseOrderNumber) ? null : order.PurchaseOrderNumber.Trim().ToUpperInvariant(),
            });
    }
}
