using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AgenticCommerce.Orders;

/// <summary>
/// Finds the agent checkout an order came from and sends the update through its protocol's publisher.
/// </summary>
public sealed class AgentOrderUpdates
{
    private readonly IAgentCheckoutStore _checkouts;
    private readonly IEnumerable<IAgentOrderPublisher> _publishers;
    private readonly ILogger _logger;

    public AgentOrderUpdates(
        IAgentCheckoutStore checkouts,
        IEnumerable<IAgentOrderPublisher> publishers,
        ILogger<AgentOrderUpdates> logger)
    {
        _checkouts = checkouts;
        _publishers = publishers;
        _logger = logger;
    }

    /// <summary>
    /// Sends an order's update to the platform whose agent placed it; orders not placed by an agent are skipped.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="created">Whether this is the order's first update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task PublishAsync(Order order, bool created, CancellationToken cancellationToken = default)
    {
        if (order is null || !string.Equals(order.Channel, AgenticCommerceStorage.OrderChannel, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var checkout = await _checkouts.FindByOrderIdAsync(order.ItemId, cancellationToken);

            if (checkout is null)
            {
                return;
            }

            foreach (var publisher in _publishers.Where(entry => string.Equals(entry.Protocol, checkout.Protocol, StringComparison.Ordinal)))
            {
                await publisher.PublishAsync(checkout, order, created, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // An order update is a notification: failing to queue one never fails the work that changed the order.
            _logger.LogError(exception, "The agent order update for order '{OrderNumber}' couldn't be queued.", order.OrderNumber);
        }
    }
}
