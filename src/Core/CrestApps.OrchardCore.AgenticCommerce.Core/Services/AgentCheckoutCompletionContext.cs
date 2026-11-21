using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// What an <see cref="IAgentCheckoutCompletionGate"/> decides on.
/// </summary>
public sealed class AgentCheckoutCompletionContext
{
    /// <summary>
    /// Gets or sets who completes the checkout, through which protocol.
    /// </summary>
    public AgentCheckoutContext Context { get; set; }

    /// <summary>
    /// Gets or sets the checkout.
    /// </summary>
    public AgentCheckout Checkout { get; set; }

    /// <summary>
    /// Gets or sets the payment the agent completes it with, with the evidence it carries (such as an AP2 mandate).
    /// </summary>
    public AgentPayment Payment { get; set; }

    /// <summary>
    /// Gets or sets the changes the complete request asks for, or <see langword="null"/>.
    /// </summary>
    public AgentCheckoutUpdate Update { get; set; }

    /// <summary>
    /// Gets or sets the checkout as it will be paid; set only for <see cref="IAgentCheckoutCompletionGate.CheckAsync"/>.
    /// </summary>
    public AgentCheckoutView View { get; set; }
}
