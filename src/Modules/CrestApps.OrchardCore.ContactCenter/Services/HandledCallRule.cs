using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Decides whether a call is one an agent actually handled, and so has a disposition owed for it.
/// </summary>
/// <remarks>
/// The agent workspace and the bar offer the activity of such a call for completion, and completing it is authorized
/// by the same rule, so what the agent is offered is what they are allowed to do.
/// </remarks>
internal static class HandledCallRule
{
    /// <summary>
    /// Determines whether the call ended normally after it was answered, and was answered by someone rather than by the
    /// dialer still reaching for an agent or by a machine.
    /// </summary>
    /// <param name="interaction">The call.</param>
    /// <returns><see langword="true"/> when the call was handled; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// A failed or never-answered call -- an unanswered inbound ring, a busy or failed dial -- was not handled. Nor is a
    /// dialer call a machine answered, or one the customer hung up on before the agent was connected: the dialer
    /// dispositions those itself.
    /// </remarks>
    public static bool IsHandledCall(Interaction interaction)
        => interaction is not null &&
            interaction.Status == InteractionStatus.Ended &&
            interaction.AnsweredUtc.HasValue &&
            !string.IsNullOrEmpty(interaction.ActivityItemId) &&
            !DialerCallMetadata.IsAwaitingAgent(interaction) &&
            !DialerCallMetadata.WasAnsweredByMachine(interaction);

    /// <summary>
    /// Determines whether the agent handled the call.
    /// </summary>
    /// <param name="interaction">The call.</param>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <returns><see langword="true"/> when the call was handled and the agent took it; otherwise <see langword="false"/>.</returns>
    public static bool IsHandledBy(Interaction interaction, string agentId)
        => !string.IsNullOrEmpty(agentId) &&
            IsHandledCall(interaction) &&
            string.Equals(interaction.AgentId, agentId, StringComparison.Ordinal);
}
