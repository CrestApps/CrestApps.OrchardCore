using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Stores orders in their own YesSql collection.
/// </summary>
public interface IOrderStore
{
    ValueTask<Order> FindByIdAsync(string orderId, CancellationToken cancellationToken = default);

    Task<Order> FindByNumberAsync(string orderNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the orders made from a cart that are not paid yet.
    /// </summary>
    Task<IReadOnlyList<Order>> ListUnpaidByCartAsync(string cartId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a page of orders, newest first.
    /// </summary>
    /// <param name="query">What to match.</param>
    /// <param name="skip">How many matching orders to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<(int Count, IReadOnlyList<Order> Orders)> PageAsync(OrderQuery query, int skip, int take, CancellationToken cancellationToken = default);

    ValueTask CreateAsync(Order order, CancellationToken cancellationToken = default);

    ValueTask UpdateAsync(Order order, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a list of orders matches.
/// </summary>
public sealed class OrderQuery
{
    /// <summary>
    /// Gets or sets the statuses to match, or none for every status.
    /// </summary>
    public IReadOnlyCollection<OrderStatus> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the user whose orders to list.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets an order number, or the start of one, or a customer's email address.
    /// </summary>
    public string Search { get; set; }
}

public sealed class OrderStore : DocumentCatalog<Order, OrderIndex>, IOrderStore
{
    // Completing a checkout and starting a new one from the same cart must not overwrite each other.
    protected override bool CheckConcurrency => true;

    public OrderStore(ISession session)
        : base(session)
    {
        CollectionName = OrderConstants.CollectionName;
    }

    public Task<Order> FindByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return Task.FromResult<Order>(null);
        }

        var number = orderNumber.Trim().ToUpperInvariant();

        return Session.Query<Order, OrderIndex>(index => index.OrderNumber == number, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> ListUnpaidByCartAsync(string cartId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cartId))
        {
            return [];
        }

        var orders = await Session.Query<Order, OrderIndex>(
                index => index.CartId == cartId && (index.Status == OrderStatus.Draft || index.Status == OrderStatus.AwaitingPayment),
                collection: CollectionName)
            .ListAsync(cancellationToken);

        return orders.ToArray();
    }

    public async Task<(int Count, IReadOnlyList<Order> Orders)> PageAsync(OrderQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        query ??= new OrderQuery();

        var orders = Session.Query<Order, OrderIndex>(collection: CollectionName);

        if (!string.IsNullOrEmpty(query.UserId))
        {
            orders = orders.Where(index => index.UserId == query.UserId);
        }

        if (query.Statuses is { Count: > 0 })
        {
            var statuses = query.Statuses.ToArray();

            orders = orders.Where(index => index.Status.IsIn(statuses));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();

            orders = orders.Where(index => index.OrderNumber.StartsWith(search) || index.CustomerEmail == search);
        }

        var count = await orders.CountAsync(cancellationToken);
        var page = await orders.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }
}
