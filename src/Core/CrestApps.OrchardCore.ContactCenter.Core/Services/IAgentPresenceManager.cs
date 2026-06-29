using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Coordinates agent sign-in, sign-out, presence, and queue/campaign membership for the Contact Center.
/// </summary>
public interface IAgentPresenceManager
{
    /// <summary>
    /// Signs the agent in, makes them available, and joins the supplied queues and campaigns.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="queueIds">The queues to sign in to.</param>
    /// <param name="campaignIds">The dialer campaigns to sign in to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent profile after sign-in.</returns>
    Task<AgentProfile> SignInAsync(string userId, IEnumerable<string> queueIds, IEnumerable<string> campaignIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs the agent out and takes them offline.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent profile after sign-out, or <see langword="null"/> when none exists.</returns>
    Task<AgentProfile> SignOutAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the agent presence state and optional reason code.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="status">The presence state to apply.</param>
    /// <param name="reason">The optional reason code.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent profile after the change, or <see langword="null"/> when none exists.</returns>
    Task<AgentProfile> SetPresenceAsync(string userId, AgentPresenceStatus status, string reason, CancellationToken cancellationToken = default);
}
