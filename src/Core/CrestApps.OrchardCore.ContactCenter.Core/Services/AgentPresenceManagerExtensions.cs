using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Extension methods for <see cref="IAgentPresenceManager"/>.
/// </summary>
public static class AgentPresenceManagerExtensions
{
    /// <summary>
    /// Puts the agent a dialer held for a campaign call on that call, now that they are connected to it. A call that is
    /// not a campaign dial, or an agent no dialer holds, is left as it is.
    /// </summary>
    /// <param name="presenceManager">The presence manager.</param>
    /// <param name="agentId">The agent profile identifier of the agent connected to the call.</param>
    /// <param name="interaction">The interaction of the call.</param>
    /// <param name="connectedUtc">When the agent was connected.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static async Task ConnectHeldAgentAsync(
        this IAgentPresenceManager presenceManager,
        string agentId,
        Interaction interaction,
        DateTime connectedUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presenceManager);

        if (string.IsNullOrEmpty(agentId) || !DialerCallMetadata.IsCampaignDial(interaction))
        {
            return;
        }

        await presenceManager.ConnectHeldAgentAsync(agentId, new AgentStateChangeContext
        {
            Source = AgentStateChangeSources.Connected,
            InteractionId = interaction.ItemId,
            ReservationId = DialerCallMetadata.GetReservationId(interaction),
            ChangedUtc = connectedUtc,
        }, cancellationToken);
    }
}
