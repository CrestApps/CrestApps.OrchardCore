using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// The protocol-neutral agent checkout. Every protocol adapter (UCP, ACP, MCP bindings) drives the store's own checkout
/// through it: the same prices, tax, shipping, discounts, stock holds and payment engine as the storefront.
/// </summary>
public interface IAgentCheckoutService
{
    /// <summary>
    /// Makes a checkout for the items an agent gives, applies the rest of what it gives, and returns where it stands.
    /// </summary>
    /// <param name="context">Who asks, through which protocol.</param>
    /// <param name="input">What the agent gives.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentCheckoutResult> CreateAsync(AgentCheckoutContext context, AgentCheckoutInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns where a checkout stands.
    /// </summary>
    /// <param name="context">Who asks, through which protocol.</param>
    /// <param name="id">The checkout's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentCheckoutResult> GetAsync(AgentCheckoutContext context, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a checkout and returns where it stands.
    /// </summary>
    /// <param name="context">Who asks, through which protocol.</param>
    /// <param name="id">The checkout's id.</param>
    /// <param name="update">What changes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentCheckoutResult> UpdateAsync(AgentCheckoutContext context, string id, AgentCheckoutUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pays for a checkout with an agent's credential and, once paid, places the order.
    /// </summary>
    /// <param name="context">Who asks, through which protocol.</param>
    /// <param name="id">The checkout's id.</param>
    /// <param name="payment">How the agent pays.</param>
    /// <param name="update">Last changes, such as the buyer's details, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentCheckoutResult> CompleteAsync(AgentCheckoutContext context, string id, AgentPayment payment, AgentCheckoutUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a checkout, which releases the stock it held.
    /// </summary>
    /// <param name="context">Who asks, through which protocol.</param>
    /// <param name="id">The checkout's id.</param>
    /// <param name="intentTrace">Why the agent canceled, as it gave it, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentCheckoutResult> CancelAsync(AgentCheckoutContext context, string id, JsonObject intentTrace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prices items as the store would check them out, without making a checkout or holding stock, as a cart's estimate.
    /// </summary>
    /// <param name="context">Who asks, through which protocol.</param>
    /// <param name="lines">The items.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentCheckoutView> PreviewAsync(AgentCheckoutContext context, IList<AgentCheckoutLineInput> lines, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the payment handlers an agent may pay a checkout through.
    /// </summary>
    /// <param name="context">Who asks, through which protocol.</param>
    /// <param name="view">The checkout, or <see langword="null"/> to describe the store.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AgentPaymentHandlerDescriptor>> GetPaymentHandlersAsync(AgentCheckoutContext context, AgentCheckoutView view, CancellationToken cancellationToken = default);

    /// <summary>
    /// Expires the open checkouts whose time ran out, which releases the stock they held.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<int> ExpireAsync(CancellationToken cancellationToken = default);
}
