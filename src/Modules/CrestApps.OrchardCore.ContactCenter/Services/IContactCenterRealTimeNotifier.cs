using CrestApps.OrchardCore.ContactCenter.Hubs;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Broadcasts Contact Center real-time updates to the appropriate SignalR audiences (the affected agent,
/// queue watchers, and supervisors) without exposing the underlying hub or group naming to callers.
/// </summary>
public interface IContactCenterRealTimeNotifier
{
    /// <summary>
    /// Broadcasts an agent presence change to the agent's own connections and to supervisors.
    /// </summary>
    /// <param name="notification">The presence change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task NotifyPresenceChangedAsync(AgentPresenceNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a work offer to the target agent, the originating queue watchers, and supervisors.
    /// </summary>
    /// <param name="notification">The offer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task NotifyOfferReceivedAsync(AgentOfferNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts the revocation of a work offer to the target agent, the originating queue watchers, and supervisors.
    /// </summary>
    /// <param name="notification">The revoked offer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task NotifyOfferRevokedAsync(AgentOfferRevokedNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a queue depth change to the queue's watchers and to supervisors.
    /// </summary>
    /// <param name="notification">The updated queue statistics.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task NotifyQueueStatsChangedAsync(QueueStatsNotification notification, CancellationToken cancellationToken = default);
}
