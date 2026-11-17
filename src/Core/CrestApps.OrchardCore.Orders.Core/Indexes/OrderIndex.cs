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

    /// <summary>
    /// Gets or sets when the order was paid; <see langword="null"/> until it is, and for an order indexed before this
    /// column was added and not saved since.
    /// </summary>
    public DateTime? PaidUtc { get; set; }

    /// <summary>
    /// Gets or sets when the order last committed part of its account's budget: when its approval was asked for (while
    /// it is waiting or approved) or its payment hold was made, whichever is later; <see langword="null"/> when neither.
    /// </summary>
    public DateTime? BudgetCommittedUtc { get; set; }

    public DateTime? AnonymizedUtc { get; set; }

    public string Channel { get; set; }

    public string LocationId { get; set; }

    public string ShiftId { get; set; }

    public string AccountId { get; set; }

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
                PaidUtc = order.PaidUtc,
                BudgetCommittedUtc = GetBudgetCommittedUtc(order),
                Channel = order.Channel,
                LocationId = order.LocationId,
                ShiftId = order.ShiftId,
                AccountId = order.AccountId,
                PurchaseOrderNumber = string.IsNullOrWhiteSpace(order.PurchaseOrderNumber) ? null : order.PurchaseOrderNumber.Trim().ToUpperInvariant(),
            });
    }

    // A time the order kept, or when it was made for one that didn't keep it, as the budget reads them.
    private static DateTime? GetBudgetCommittedUtc(Order order)
    {
        DateTime? requested = order.Approval is { Status: OrderApprovalStatus.Pending or OrderApprovalStatus.Approved } approval
            ? Since(approval.RequestedUtc, order)
            : null;
        DateTime? held = order.BudgetHold is { } hold ? Since(hold.HeldUtc, order) : null;

        return requested > held || held is null ? requested : held;
    }

    private static DateTime Since(DateTime utc, Order order)
        => utc == default ? order.CreatedUtc : utc;
}
