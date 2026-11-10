using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Orders;

/// <summary>
/// Sends a protocol's order updates for the orders its agents placed.
/// </summary>
public interface IAgentOrderPublisher
{
    /// <summary>
    /// Gets the protocol whose agents' orders it sends updates for.
    /// </summary>
    string Protocol { get; }

    /// <summary>
    /// Sends the order's full current state.
    /// </summary>
    /// <param name="checkout">The agent checkout that placed the order.</param>
    /// <param name="order">The order.</param>
    /// <param name="created">Whether this is the order's first update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task PublishAsync(AgentCheckout checkout, Order order, bool created, CancellationToken cancellationToken = default);
}
