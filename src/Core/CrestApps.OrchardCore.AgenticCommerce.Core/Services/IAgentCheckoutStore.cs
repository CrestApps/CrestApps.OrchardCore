using CrestApps.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Stores agent checkouts.
/// </summary>
public interface IAgentCheckoutStore : ICatalog<AgentCheckout>
{
    /// <summary>
    /// Returns the agent checkout of an order, or <see langword="null"/>.
    /// </summary>
    /// <param name="orderId">The order's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentCheckout> FindByOrderIdAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the agent checkout a store checkout holds, or <see langword="null"/>.
    /// </summary>
    /// <param name="checkoutSessionId">The store checkout's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentCheckout> FindBySessionIdAsync(string checkoutSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns open agent checkouts that expired before a time.
    /// </summary>
    /// <param name="utcNow">The time.</param>
    /// <param name="take">How many at most.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AgentCheckout>> ListExpiredAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns how many open agent checkouts a platform has.
    /// </summary>
    /// <param name="platformId">The platform's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<int> CountOpenAsync(string platformId, CancellationToken cancellationToken = default);
}
