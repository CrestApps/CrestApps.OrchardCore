using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// What an agent checkout call did.
/// </summary>
public sealed class AgentCheckoutResult
{
    /// <summary>
    /// Gets the checkout.
    /// </summary>
    public AgentCheckout Checkout { get; init; }

    /// <summary>
    /// Gets where the checkout stands.
    /// </summary>
    public AgentCheckoutView View { get; init; }

    /// <summary>
    /// Gets why the call couldn't be made, or <see langword="null"/> when it was.
    /// </summary>
    public AgentCheckoutFailure? Failure { get; init; }

    /// <summary>
    /// Gets what went wrong, for the agent.
    /// </summary>
    public string Error { get; init; }

    /// <summary>
    /// Gets a value indicating whether the call was made. Business problems, such as a declined card, are in the view's
    /// messages, not here.
    /// </summary>
    public bool Succeeded
        => Failure is null;

    /// <summary>
    /// Returns a result for a call that couldn't be made.
    /// </summary>
    /// <param name="failure">Why.</param>
    /// <param name="error">What went wrong.</param>
    /// <param name="checkout">The checkout, when there is one.</param>
    /// <param name="view">Where it stands, when known.</param>
    public static AgentCheckoutResult Fail(AgentCheckoutFailure failure, string error, AgentCheckout checkout = null, AgentCheckoutView view = null)
        => new()
        {
            Failure = failure,
            Error = error,
            Checkout = checkout,
            View = view,
        };
}
