using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Orders.Core.Indexes;

public sealed class OrderReturnIndex : CatalogItemIndex
{
    public string Number { get; set; }

    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    public string UserId { get; set; }

    public OrderReturnStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class OrderReturnIndexProvider : IndexProvider<OrderReturn>
{
    public OrderReturnIndexProvider()
    {
        CollectionName = OrderConstants.CollectionName;
    }

    public override void Describe(DescribeContext<OrderReturn> context)
    {
        context.For<OrderReturnIndex>()
            .Map(orderReturn => new OrderReturnIndex
            {
                ItemId = orderReturn.ItemId,
                Number = orderReturn.Number,
                OrderId = orderReturn.OrderId,
                OrderNumber = orderReturn.OrderNumber,
                UserId = orderReturn.UserId,
                Status = orderReturn.Status,
                CreatedUtc = orderReturn.CreatedUtc,
            });
    }
}
