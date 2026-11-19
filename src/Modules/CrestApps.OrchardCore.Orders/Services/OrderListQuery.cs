using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.ViewModels;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// The search, status and channel of Commerce › Orders as a query, shared by the list, its CSV export and the order AI
/// tools. A customer's own orders (My Orders) are the paid and awaiting-payment ones placed by them, unsearched.
/// </summary>
internal static class OrderListQuery
{
    /// <summary>
    /// Builds the query of the orders list.
    /// </summary>
    /// <param name="status">The status filter; <see cref="OrderStatusFilter.Placed"/> is paid or awaiting payment.</param>
    /// <param name="channel">The channel: <see cref="OrderChannels.Online"/>, <see cref="OrderChannels.PointOfSale"/>,
    /// <see cref="OrderChannels.Draft"/> or <see cref="OrderChannels.Agent"/>; anything else is every channel.</param>
    /// <param name="search">The order number or customer email to find.</param>
    /// <param name="userId">The customer whose own orders are listed, or <see langword="null"/> for the store's.</param>
    /// <returns>The query.</returns>
    public static OrderQuery Build(OrderStatusFilter status, string channel, string search, string userId)
        => new()
        {
            UserId = userId,
            Search = userId is null ? search : null,
            Channel = userId is null && channel is OrderChannels.Online or OrderChannels.PointOfSale or OrderChannels.Draft or OrderChannels.Agent ? channel : null,
            Statuses = userId is not null
                ? [OrderStatus.Paid, OrderStatus.AwaitingPayment]
                : status switch
                {
                    OrderStatusFilter.Paid => [OrderStatus.Paid],
                    OrderStatusFilter.AwaitingPayment => [OrderStatus.AwaitingPayment],
                    OrderStatusFilter.Canceled => [OrderStatus.Canceled],
                    OrderStatusFilter.Drafts => [OrderStatus.Draft],
                    OrderStatusFilter.All => [],
                    _ => [OrderStatus.Paid, OrderStatus.AwaitingPayment],
                },
        };
}
