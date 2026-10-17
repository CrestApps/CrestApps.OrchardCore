using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Hears about what happens to orders, so modules can act on it, such as releasing the stock an order held.
/// </summary>
public interface IOrderEventHandler
{
    /// <summary>
    /// Runs after an unpaid order was canceled, in the same unit of work.
    /// </summary>
    Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default);
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
