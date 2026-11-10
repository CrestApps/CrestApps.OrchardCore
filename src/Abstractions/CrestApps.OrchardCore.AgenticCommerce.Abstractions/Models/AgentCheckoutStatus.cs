namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// Where an agent checkout stands, in terms every agentic commerce protocol maps to its own.
/// </summary>
public enum AgentCheckoutStatus
{
    /// <summary>
    /// Something the agent can give is missing or wrong; the messages say what.
    /// </summary>
    Incomplete,

    /// <summary>
    /// The buyer has to finish in the store's own checkout, at the continue URL.
    /// </summary>
    RequiresEscalation,

    /// <summary>
    /// Everything is known; the agent may complete it with a payment.
    /// </summary>
    ReadyForComplete,

    /// <summary>
    /// The payment was taken and the provider hasn't confirmed it yet.
    /// </summary>
    CompleteInProgress,

    /// <summary>
    /// Paid; the order exists.
    /// </summary>
    Completed,

    /// <summary>
    /// Canceled by the agent or the store.
    /// </summary>
    Canceled,

    /// <summary>
    /// Not completed in time.
    /// </summary>
    Expired,
}
