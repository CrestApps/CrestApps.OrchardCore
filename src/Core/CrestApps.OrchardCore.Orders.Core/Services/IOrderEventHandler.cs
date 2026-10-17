using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Hears about what happens to orders, so modules can act on it, such as holding stock for an order, selling it when
/// the order is paid, and releasing it when the order is canceled. Derive from <see cref="OrderEventHandlerBase"/> to
/// handle only some of the events.
/// </summary>
public interface IOrderEventHandler
{
    /// <summary>
    /// Runs after an order is made from a cart, before its checkout starts. A handler that can't provide what the
    /// order needs refuses it, and the order is canceled.
    /// </summary>
    Task CreatedAsync(OrderCreatedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs when the order's checkout completes, in the same unit of work that marks it paid. A handler that throws
    /// rolls the completion back, and the checkout retries it.
    /// </summary>
    Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs after an unpaid order was canceled, in the same unit of work.
    /// </summary>
    Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default);
}

public abstract class OrderEventHandlerBase : IOrderEventHandler
{
    public virtual Task CreatedAsync(OrderCreatedContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class OrderCreatedContext
{
    private readonly List<string> _problems = [];

    public OrderCreatedContext(Order order)
    {
        Order = order;
    }

    public Order Order { get; }

    /// <summary>
    /// Gets why the order was refused, for the buyer, or nothing when it wasn't.
    /// </summary>
    public IReadOnlyList<string> Problems
        => _problems;

    /// <summary>
    /// Refuses the order, saying why to the buyer.
    /// </summary>
    public void Refuse(string problem)
    {
        ArgumentException.ThrowIfNullOrEmpty(problem);

        _problems.Add(problem);
    }
}

public sealed class OrderPaidContext
{
    public OrderPaidContext(Order order)
    {
        Order = order;
    }

    public Order Order { get; }
}

public sealed class OrderCanceledContext
{
    public OrderCanceledContext(Order order, OrderCancelReason reason)
    {
        Order = order;
        Reason = reason;
    }

    public Order Order { get; }

    public OrderCancelReason Reason { get; }
}
