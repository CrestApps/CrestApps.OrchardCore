namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// Who can resolve an agent checkout error.
/// </summary>
public enum AgentMessageSeverity
{
    /// <summary>
    /// The agent can fix it by updating the checkout.
    /// </summary>
    Recoverable,

    /// <summary>
    /// The buyer has to give something.
    /// </summary>
    RequiresBuyerInput,

    /// <summary>
    /// The buyer has to review something, such as a policy.
    /// </summary>
    RequiresBuyerReview,

    /// <summary>
    /// Nobody can fix it; the checkout can't complete as it is.
    /// </summary>
    Unrecoverable,
}
