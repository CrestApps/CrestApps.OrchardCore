using CrestApps.OrchardCore.AgenticCommerce.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Services;

/// <summary>
/// A way an AI agent pays: a payment handler in the agentic commerce protocols. It tells the agent how to get a
/// credential, and turns the credential into a payment the store's checkout engine takes.
/// </summary>
public interface IAgentPaymentHandler
{
    /// <summary>
    /// Gets the handler's name in a protocol, such as <c>com.stripe.payments</c> in UCP or
    /// <c>dev.acp.tokenized.card</c> in ACP.
    /// </summary>
    /// <param name="protocol">The protocol, one of <see cref="AgenticCommerceConstants.Protocols"/>.</param>
    string GetName(string protocol);

    /// <summary>
    /// Describes the handler for an agent, or returns <see langword="null"/> when it can't take payments now (it isn't
    /// set up, or not for this protocol, currency or platform).
    /// </summary>
    /// <param name="context">Who asks, and for which checkout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentPaymentHandlerDescriptor> DescribeAsync(AgentPaymentHandlerContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns an agent's credential into the provider and data the checkout engine charges.
    /// </summary>
    /// <param name="context">Who pays, and for which checkout.</param>
    /// <param name="payment">The agent's payment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentPaymentPreparation> PrepareAsync(AgentPaymentHandlerContext context, AgentPayment payment, CancellationToken cancellationToken = default);
}
