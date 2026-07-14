using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Hubs;

/// <summary>
/// Defines the strongly-typed methods the Contact Center hub invokes on connected clients. The agent
/// desktop and supervisor dashboards implement these callbacks to react to live contact center activity.
/// </summary>
public interface IContactCenterHubClient
{
    /// <summary>
    /// Notifies the client that an agent's presence changed.
    /// </summary>
    /// <param name="notification">The presence change.</param>
    Task PresenceChanged(AgentPresenceNotification notification);

    /// <summary>
    /// Notifies the client that a work item is being offered to the agent.
    /// </summary>
    /// <param name="notification">The offer.</param>
    Task OfferReceived(AgentOfferNotification notification);

    /// <summary>
    /// Notifies the client that an offer is no longer presented to the agent.
    /// </summary>
    /// <param name="notification">The revoked offer.</param>
    Task OfferRevoked(AgentOfferRevokedNotification notification);

    /// <summary>
    /// Notifies the client that a queue's waiting depth changed.
    /// </summary>
    /// <param name="notification">The updated queue statistics.</param>
    Task QueueStatsChanged(QueueStatsNotification notification);

    /// <summary>
    /// Notifies the client that its queue or campaign memberships changed outside the current connection.
    /// </summary>
    Task MembershipChanged();
}
