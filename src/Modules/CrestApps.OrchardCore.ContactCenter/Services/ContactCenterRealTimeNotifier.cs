using CrestApps.OrchardCore.ContactCenter.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Provides the default <see cref="IContactCenterRealTimeNotifier"/> implementation over the
/// <see cref="ContactCenterHub"/> strongly-typed hub context.
/// </summary>
public sealed class ContactCenterRealTimeNotifier : IContactCenterRealTimeNotifier
{
    private readonly IHubContext<ContactCenterHub, IContactCenterHubClient> _hubContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRealTimeNotifier"/> class.
    /// </summary>
    /// <param name="hubContext">The Contact Center hub context used to push events to connected clients.</param>
    public ContactCenterRealTimeNotifier(IHubContext<ContactCenterHub, IContactCenterHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <inheritdoc/>
    public async Task NotifyPresenceChangedAsync(AgentPresenceNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!string.IsNullOrEmpty(notification.UserId))
        {
            await _hubContext.Clients.User(notification.UserId).PresenceChanged(notification);
        }

        await _hubContext.Clients.Group(ContactCenterHub.SupervisorsGroup).PresenceChanged(notification);
    }

    /// <inheritdoc/>
    public async Task NotifyOfferReceivedAsync(AgentOfferNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!string.IsNullOrEmpty(notification.UserId))
        {
            await _hubContext.Clients.User(notification.UserId).OfferReceived(notification);
        }

        if (!string.IsNullOrEmpty(notification.QueueId))
        {
            await _hubContext.Clients.Group(ContactCenterHub.QueueGroup(notification.QueueId)).OfferReceived(notification);
        }

        await _hubContext.Clients.Group(ContactCenterHub.SupervisorsGroup).OfferReceived(notification);
    }

    /// <inheritdoc/>
    public async Task NotifyOfferRevokedAsync(AgentOfferRevokedNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!string.IsNullOrEmpty(notification.UserId))
        {
            await _hubContext.Clients.User(notification.UserId).OfferRevoked(notification);
        }

        if (!string.IsNullOrEmpty(notification.QueueId))
        {
            await _hubContext.Clients.Group(ContactCenterHub.QueueGroup(notification.QueueId)).OfferRevoked(notification);
        }

        await _hubContext.Clients.Group(ContactCenterHub.SupervisorsGroup).OfferRevoked(notification);
    }

    /// <inheritdoc/>
    public async Task NotifyQueueStatsChangedAsync(QueueStatsNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!string.IsNullOrEmpty(notification.QueueId))
        {
            await _hubContext.Clients.Group(ContactCenterHub.QueueGroup(notification.QueueId)).QueueStatsChanged(notification);
        }

        await _hubContext.Clients.Group(ContactCenterHub.SupervisorsGroup).QueueStatsChanged(notification);
    }
}
