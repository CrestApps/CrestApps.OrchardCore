using CrestApps.OrchardCore.AgenticCommerce.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Runs the completion gates (such as AP2's mandate check) of the agent checkout, in order, and returns the first
/// refusal.
/// </summary>
internal static class AgentCheckoutCompletionGates
{
    /// <summary>
    /// Returns the first gate's refusal of what a complete request would change, or <see langword="null"/>.
    /// </summary>
    /// <param name="gates">The gates.</param>
    /// <param name="completion">The completion, with the request's changes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<AgentCheckoutMessage> CheckChangesAsync(IEnumerable<IAgentCheckoutCompletionGate> gates, AgentCheckoutCompletionContext completion, CancellationToken cancellationToken)
    {
        foreach (var gate in gates)
        {
            if (await gate.CheckChangesAsync(completion, cancellationToken) is { } refusal)
            {
                return refusal;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks the checkout exactly as it will be paid, and returns the refused completion, with the first gate's
    /// reason in its view's messages, or <see langword="null"/> when every gate agreed.
    /// </summary>
    /// <param name="gates">The gates.</param>
    /// <param name="completion">The completion, with the checkout's final view.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<AgentCheckoutResult> CheckAsync(IEnumerable<IAgentCheckoutCompletionGate> gates, AgentCheckoutCompletionContext completion, CancellationToken cancellationToken)
    {
        foreach (var gate in gates)
        {
            if (await gate.CheckAsync(completion, cancellationToken) is { } refusal)
            {
                completion.View.Messages.Add(refusal);

                return AgentCheckoutResult.Fail(AgentCheckoutFailure.Refused, error: null, completion.Checkout, completion.View);
            }
        }

        return null;
    }
}
