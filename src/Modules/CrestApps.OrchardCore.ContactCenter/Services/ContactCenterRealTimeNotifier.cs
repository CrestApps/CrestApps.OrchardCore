using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.SignalR;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Provides the default <see cref="IContactCenterRealTimeNotifier"/> implementation over the
/// <see cref="ContactCenterHub"/> strongly-typed hub context.
/// </summary>
public sealed class ContactCenterRealTimeNotifier : IContactCenterRealTimeNotifier
{
    private readonly IHubContext<ContactCenterHub, IContactCenterHubClient> _hubContext;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRealTimeNotifier"/> class.
    /// </summary>
    /// <param name="hubContext">The Contact Center hub context used to push events to connected clients.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    public ContactCenterRealTimeNotifier(
        IHubContext<ContactCenterHub, IContactCenterHubClient> hubContext,
        ShellSettings shellSettings)
    {
        _hubContext = hubContext;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public async Task NotifyPresenceChangedAsync(AgentPresenceNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!string.IsNullOrEmpty(notification.UserId))
        {
            await _hubContext.Clients.Group(UserGroup(notification.UserId)).PresenceChanged(notification);
        }

        await _hubContext.Clients.Group(SupervisorsGroup).PresenceChanged(notification);
    }

    /// <inheritdoc/>
    public async Task NotifyOfferReceivedAsync(AgentOfferNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!string.IsNullOrEmpty(notification.UserId))
        {
            await _hubContext.Clients.Group(UserGroup(notification.UserId)).OfferReceived(notification);
        }

        var observerNotification = CreateObserverNotification(notification);

        if (!string.IsNullOrEmpty(notification.QueueId))
        {
            await _hubContext.Clients.Group(QueueGroup(notification.QueueId)).OfferReceived(observerNotification);
        }

        await _hubContext.Clients.Group(SupervisorsGroup).OfferReceived(observerNotification);
    }

    /// <inheritdoc/>
    public async Task NotifyOfferRevokedAsync(AgentOfferRevokedNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!string.IsNullOrEmpty(notification.UserId))
        {
            await _hubContext.Clients.Group(UserGroup(notification.UserId)).OfferRevoked(notification);
        }

        if (!string.IsNullOrEmpty(notification.QueueId))
        {
            await _hubContext.Clients.Group(QueueGroup(notification.QueueId)).OfferRevoked(notification);
        }

        await _hubContext.Clients.Group(SupervisorsGroup).OfferRevoked(notification);
    }

    /// <inheritdoc/>
    public async Task NotifyQueueStatsChangedAsync(QueueStatsNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!string.IsNullOrEmpty(notification.QueueId))
        {
            await _hubContext.Clients.Group(QueueGroup(notification.QueueId)).QueueStatsChanged(notification);
        }

        await _hubContext.Clients.Group(SupervisorsGroup).QueueStatsChanged(notification);
    }

    private string SupervisorsGroup
    {
        get
        {
            return TenantSignalRGroupName.ForGroup(_tenantName, ContactCenterHub.SupervisorsGroup);
        }
    }

    private string QueueGroup(string queueId)
    {
        return TenantSignalRGroupName.ForGroup(_tenantName, ContactCenterHub.QueueGroup(queueId));
    }

    private string UserGroup(string userId)
    {
        return TenantSignalRGroupName.ForUser(_tenantName, userId);
    }

    private static AgentOfferNotification CreateObserverNotification(AgentOfferNotification notification)
    {
        return new AgentOfferNotification
        {
            UserId = notification.UserId,
            AgentId = notification.AgentId,
            ReservationId = notification.ReservationId,
            ActivityItemId = notification.ActivityItemId,
            QueueItemId = notification.QueueItemId,
            QueueId = notification.QueueId,
            ExpiresUtc = notification.ExpiresUtc,
            ServerTimeUtc = notification.ServerTimeUtc,
        };
    }
}
