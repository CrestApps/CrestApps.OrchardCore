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
    /// Runs after an order was canceled, in the same unit of work: an unpaid one whose checkout ended, or any one the
    /// store canceled.
    /// </summary>
    Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs after money was given back for the order and recorded on it.
    /// </summary>
    Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Runs when items the buyer sent back arrived, in the same unit of work, such as to put them back into stock.
    /// </summary>
    Task ReturnedAsync(OrderReturnedContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Runs when the lines of an order not checked out yet were replaced, such as a store's draft being changed, in the
    /// same unit of work. A handler that can't provide the new lines refuses them, and nothing changes.
    /// </summary>
    Task LinesChangedAsync(OrderLinesChangedContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public abstract class OrderEventHandlerBase : IOrderEventHandler
{
    public virtual Task CreatedAsync(OrderCreatedContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task ReturnedAsync(OrderReturnedContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task LinesChangedAsync(OrderLinesChangedContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// The lines of an order not checked out yet, just replaced.
/// </summary>
public sealed class OrderLinesChangedContext
{
    private readonly List<string> _problems = [];

    public OrderLinesChangedContext(Order order, IReadOnlyList<OrderLine> previousLines)
    {
        Order = order;
        PreviousLines = previousLines ?? [];
    }

    public Order Order { get; }

    public IReadOnlyList<OrderLine> PreviousLines { get; }

    public IReadOnlyList<string> Problems
        => _problems;

    /// <summary>
    /// Refuses the new lines, with why, for the store.
    /// </summary>
    public void Refuse(string problem)
        => _problems.Add(problem);
}

/// <summary>
/// Items of an order that came back.
/// </summary>
public sealed class OrderReturnedContext
{
    public OrderReturnedContext(Order order, OrderReturn orderReturn, IReadOnlyList<OrderReturnedLine> lines)
    {
        Order = order;
        Return = orderReturn;
        Lines = lines;
    }

    public Order Order { get; }

    public OrderReturn Return { get; }

    /// <summary>
    /// Gets the units of each line that came back.
    /// </summary>
    public IReadOnlyList<OrderReturnedLine> Lines { get; }

    /// <summary>
    /// Gets or sets the location the units go back into stock at, or <see langword="null"/> for where they were sold
    /// from.
    /// </summary>
    public string LocationId { get; set; }

    public string ActorId { get; set; }

    public string ActorName { get; set; }
}

/// <summary>
/// Units of an order line that came back, and whether they can be sold again.
/// </summary>
public sealed record OrderReturnedLine(OrderLine Line, int Quantity, bool Restock);

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

    /// <summary>
    /// Gets or sets a value indicating whether what the order sold goes back into stock.
    /// </summary>
    public bool Restock { get; set; }
}

public sealed class OrderRefundedContext
{
    public OrderRefundedContext(Order order, OrderRefund refund)
    {
        Order = order;
        Refund = refund;
    }

    public Order Order { get; }

    /// <summary>
    /// Gets the refund just recorded.
    /// </summary>
    public OrderRefund Refund { get; }

    /// <summary>
    /// Gets or sets a value indicating whether what the order sold goes back into stock, which the store chooses
    /// when it refunds everything.
    /// </summary>
    public bool Restock { get; set; }
}
