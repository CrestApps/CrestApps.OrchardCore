using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// The order made from a cart, or why it couldn't be: then the order is canceled and <see cref="Problems"/> says why.
/// </summary>
public sealed class OrderCreationResult
{
    public OrderCreationResult(Order order, IReadOnlyList<string> problems)
    {
        Order = order;
        Problems = problems ?? [];
    }

    public Order Order { get; }

    public IReadOnlyList<string> Problems { get; }

    public bool Succeeded
        => Problems.Count == 0;
}
