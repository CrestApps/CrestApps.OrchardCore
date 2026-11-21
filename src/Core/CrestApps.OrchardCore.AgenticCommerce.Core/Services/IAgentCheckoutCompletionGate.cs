using CrestApps.OrchardCore.AgenticCommerce.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Has a say when an agent completes a checkout, inside <see cref="IAgentCheckoutService.CompleteAsync"/>, so every
/// transport (REST, MCP, A2A, ACP) passes it: it can refuse the changes a complete request makes, and refuse to pay the
/// checkout as it finally stands, such as when it isn't what the buyer approved in a payment mandate.
/// </summary>
public interface IAgentCheckoutCompletionGate
{
    /// <summary>
    /// Called before a complete request's own changes (buyer, fulfillment, discount codes) are applied. Returns why
    /// they are refused, or <see langword="null"/>.
    /// </summary>
    /// <param name="context">The checkout, the request and its changes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentCheckoutMessage> CheckChangesAsync(AgentCheckoutCompletionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after the checkout's final state is known and before it is paid. Returns why it may not be paid, or
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="context">The checkout as it will be paid, and the payment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentCheckoutMessage> CheckAsync(AgentCheckoutCompletionContext context, CancellationToken cancellationToken = default);
}
