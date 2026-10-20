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
    /// Gets or sets the customer whose orders to find.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the email the orders were placed with, matched exactly, ignoring case.
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Gets or sets whether to find only orders placed without an account.
    /// </summary>
    public bool GuestsOnly { get; set; }

    /// <summary>
    /// Gets or sets the date before which the orders were placed.
    /// </summary>
    public DateTime? CreatedBefore { get; set; }

    /// <summary>
    /// Gets or sets the date on or after which the orders were placed.
    /// </summary>
    public DateTime? CreatedOnOrAfter { get; set; }

    /// <summary>
    /// Gets or sets whether to leave out orders whose personal details were already removed.
    /// </summary>
    public bool NotAnonymized { get; set; }

    /// <summary>
    /// Gets or sets an order number, or the start of one, or a customer's email address.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the channel to match, such as <see cref="OrderChannels.PointOfSale"/>, or none for every channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the inventory location to match, or none for every location.
    /// </summary>
    public string LocationId { get; set; }

    /// <summary>
    /// Gets or sets the register shift to match, or none.
    /// </summary>
    public string ShiftId { get; set; }
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

        if (!string.IsNullOrEmpty(query.CustomerId))
        {
            orders = orders.Where(index => index.CustomerId == query.CustomerId);
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerEmail))
        {
            var email = query.CustomerEmail.Trim().ToUpperInvariant();

            orders = orders.Where(index => index.CustomerEmail == email);
        }

        if (query.GuestsOnly)
        {
            orders = orders.Where(index => index.UserId == null);
        }

        if (query.CreatedBefore is { } createdBefore)
        {
            orders = orders.Where(index => index.CreatedUtc < createdBefore);
        }

        if (query.CreatedOnOrAfter is { } createdOnOrAfter)
        {
            orders = orders.Where(index => index.CreatedUtc >= createdOnOrAfter);
        }

        if (query.NotAnonymized)
        {
            orders = orders.Where(index => index.AnonymizedUtc == null);
        }

        if (!string.IsNullOrEmpty(query.Channel))
        {
            var channel = query.Channel;

            // Orders saved before channels were kept have none, and were all made online.
            orders = string.Equals(channel, OrderChannels.Online, StringComparison.Ordinal)
                ? orders.Where(index => index.Channel == channel || index.Channel == null)
                : orders.Where(index => index.Channel == channel);
        }

        if (!string.IsNullOrEmpty(query.LocationId))
        {
            var locationId = query.LocationId;

            orders = orders.Where(index => index.LocationId == locationId);
        }

        if (!string.IsNullOrEmpty(query.ShiftId))
        {
            var shiftId = query.ShiftId;

            orders = orders.Where(index => index.ShiftId == shiftId);
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
