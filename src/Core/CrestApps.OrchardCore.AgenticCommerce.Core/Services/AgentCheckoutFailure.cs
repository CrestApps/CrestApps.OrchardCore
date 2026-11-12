namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Why an agent checkout call couldn't be made.
/// </summary>
public enum AgentCheckoutFailure
{
    /// <summary>
    /// There is no such checkout for the caller.
    /// </summary>
    NotFound,

    /// <summary>
    /// What was asked isn't valid, such as a checkout with no items.
    /// </summary>
    Invalid,

    /// <summary>
    /// The checkout can't do that in its state, such as changing a completed checkout.
    /// </summary>
    InvalidState,

    /// <summary>
    /// Another request is changing the same checkout; try again.
    /// </summary>
    Busy,

    /// <summary>
    /// The platform has too many open checkouts.
    /// </summary>
    TooMany,

    /// <summary>
    /// The checkout waits for the result of the buyer's 3-D Secure authentication, which the request didn't carry.
    /// </summary>
    AuthenticationRequired,

    /// <summary>
    /// An <see cref="IAgentCheckoutCompletionGate"/> refused the completion; why is in the view's messages.
    /// </summary>
    Refused,
}
