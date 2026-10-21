using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Reads the orders a report counts: those paid in a period, page by page, so a report never holds a store's whole
/// history at once.
/// </summary>
public sealed class OrderReportReader
{
    private const int PageSize = 500;

    // An order is placed before it is paid; a checkout lives a day by default, and Pay Later or a slow provider can take
    // longer, so the orders placed this long before the period are read too and kept only if paid in it.
    private static readonly TimeSpan _paymentLag = TimeSpan.FromDays(7);

    private readonly IOrderStore _orderStore;

    public OrderReportReader(IOrderStore orderStore)
    {
        _orderStore = orderStore;
    }

    /// <summary>
    /// Returns the orders paid from <paramref name="fromUtc"/> (inclusive) to <paramref name="toUtc"/> (exclusive),
    /// including those canceled since: what they sold and gave back still counts.
    /// </summary>
    public async IAsyncEnumerable<Order> ReadPaidAsync(
        DateTime fromUtc,
        DateTime toUtc,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = new OrderQuery
        {
            Statuses = [OrderStatus.Paid, OrderStatus.Canceled],
            CreatedOnOrAfter = fromUtc - _paymentLag,
            CreatedBefore = toUtc,
        };

        for (var skip = 0; ; skip += PageSize)
        {
            var (_, orders) = await _orderStore.PageAsync(query, skip, PageSize, cancellationToken);

            foreach (var order in orders)
            {
                if (order.PaidUtc is { } paid && paid >= fromUtc && paid < toUtc)
                {
                    yield return order;
                }
            }

            if (orders.Count < PageSize)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Returns the paid orders with something to ship that isn't shipped in full yet. An order that ships has a
    /// shipping method; its fulfillment is recorded from its first shipment on, so one with none yet has shipped nothing.
    /// </summary>
    public async IAsyncEnumerable<Order> ReadUnfulfilledAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = new OrderQuery { Statuses = [OrderStatus.Paid] };

        for (var skip = 0; ; skip += PageSize)
        {
            var (_, orders) = await _orderStore.PageAsync(query, skip, PageSize, cancellationToken);

            foreach (var order in orders)
            {
                if (order.FulfillmentStatus is OrderFulfillmentStatus.Unfulfilled or OrderFulfillmentStatus.PartiallyFulfilled ||
                    (order.FulfillmentStatus is null && order.Shipping is not null))
                {
                    yield return order;
                }
            }

            if (orders.Count < PageSize)
            {
                yield break;
            }
        }
    }
}
