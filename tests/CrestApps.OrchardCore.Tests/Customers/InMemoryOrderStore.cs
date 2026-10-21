using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// Orders in memory, filtered the way the YesSql order store filters them.
/// </summary>
internal sealed class InMemoryOrderStore : IOrderStore
{
    public List<Order> Orders { get; } = [];

    public ValueTask<Order> FindByIdAsync(string orderId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Orders.FirstOrDefault(order => order.ItemId == orderId));

    public Task<Order> FindByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
        => Task.FromResult(Orders.FirstOrDefault(order => order.OrderNumber == orderNumber));

    public Task<IReadOnlyList<Order>> ListUnpaidByCartAsync(string cartId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Order>>([]);

    public Task<(int Count, IReadOnlyList<Order> Orders)> PageAsync(OrderQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var matches = Orders
            .Where(order => query.Statuses is not { Count: > 0 } || query.Statuses.Contains(order.Status))
            .Where(order => query.UserId is null || order.UserId == query.UserId)
            .Where(order => query.CustomerId is null || order.CustomerId == query.CustomerId)
            .Where(order => query.CustomerEmail is null || string.Equals(order.CustomerEmail, query.CustomerEmail, StringComparison.OrdinalIgnoreCase))
            .Where(order => !query.GuestsOnly || order.UserId is null)
            .Where(order => query.CreatedBefore is null || order.CreatedUtc < query.CreatedBefore)
            .Where(order => query.CreatedOnOrAfter is null || order.CreatedUtc >= query.CreatedOnOrAfter)
            .Where(order => !query.NotAnonymized || order.AnonymizedUtc is null)
            .OrderByDescending(order => order.CreatedUtc)
            .ToArray();

        return Task.FromResult<(int, IReadOnlyList<Order>)>((matches.Length, matches.Skip(skip).Take(take).ToArray()));
    }

    public ValueTask CreateAsync(Order order, CancellationToken cancellationToken = default)
    {
        Orders.Add(order);

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(Order order, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
