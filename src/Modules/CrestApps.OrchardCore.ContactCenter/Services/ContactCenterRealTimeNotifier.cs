using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.SignalR.Core;
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
    private readonly IAgentSessionManager _sessionManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRealTimeNotifier"/> class.
    /// </summary>
    /// <param name="hubContext">The Contact Center hub context used to push events to connected clients.</param>
    /// <param name="sessionManager">The agent session manager used to resolve active connections.</param>
    /// <param name="agentManager">The agent profile manager used to find the queues an agent's events concern.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    public ContactCenterRealTimeNotifier(
        IHubContext<ContactCenterHub, IContactCenterHubClient> hubContext,
        IAgentSessionManager sessionManager,
        IAgentProfileManager agentManager,
        ShellSettings shellSettings)
    {
        _hubContext = hubContext;
        _sessionManager = sessionManager;
        _agentManager = agentManager;
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

        var supervisors = await GetSupervisorGroupsAsync(notification.QueueIds, notification.AgentId, notification.UserId, cancellationToken);

        if (supervisors.Count > 0)
        {
            await _hubContext.Clients.Groups(supervisors).PresenceChanged(notification);
        }
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

        var supervisors = await GetOfferSupervisorGroupsAsync(notification.QueueId, notification.AgentId, notification.UserId, cancellationToken);

        if (supervisors.Count > 0)
        {
            await _hubContext.Clients.Groups(supervisors).OfferReceived(observerNotification);
        }
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

        var supervisors = await GetOfferSupervisorGroupsAsync(notification.QueueId, notification.AgentId, notification.UserId, cancellationToken);

        if (supervisors.Count > 0)
        {
            await _hubContext.Clients.Groups(supervisors).OfferRevoked(notification);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyQueueStatsChangedAsync(QueueStatsNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!string.IsNullOrEmpty(notification.QueueId))
        {
            await _hubContext.Clients.Group(QueueGroup(notification.QueueId)).QueueStatsChanged(notification);
            await _hubContext.Clients.Group(SupervisorQueueGroup(notification.QueueId)).QueueStatsChanged(notification);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyAgentMembershipChangedAsync(
        string userId,
        IEnumerable<string> removedQueueIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var session = await _sessionManager.FindByUserIdAsync(userId, cancellationToken);

        if (session is not null)
        {
            foreach (var connectionId in session.ConnectionIds)
            {
                foreach (var queueId in removedQueueIds.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    await _hubContext.Groups.RemoveFromGroupAsync(
                        connectionId,
                        QueueGroup(queueId),
                        cancellationToken);
                }
            }
        }

        await _hubContext.Clients.Group(UserGroup(userId)).MembershipChanged();
    }

    /// <inheritdoc/>
    public async Task NotifyRecordingStateChangedAsync(RecordingStateNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!string.IsNullOrEmpty(notification.UserId))
        {
            await _hubContext.Clients.Group(UserGroup(notification.UserId)).RecordingStateChanged(notification);
        }

        var supervisors = await GetSupervisorGroupsAsync([], notification.AgentId, notification.UserId, cancellationToken);

        if (supervisors.Count > 0)
        {
            await _hubContext.Clients.Groups(supervisors).RecordingStateChanged(notification);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyInteractionChangedAsync(AgentInteractionNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // The agent's own screens only: supervisors already follow agents through presence and their own board.
        if (string.IsNullOrEmpty(notification.UserId))
        {
            return;
        }

        await _hubContext.Clients.Group(UserGroup(notification.UserId)).InteractionChanged(notification);
    }

    /// <inheritdoc/>
    public async Task NotifyCallQualityAlertAsync(CallQualityAlertNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Supervisors only: the agent is not the one to act on it mid-shift, and is often not the cause.
        var supervisors = await GetSupervisorGroupsAsync([], notification.AgentId, notification.UserId, cancellationToken);

        if (supervisors.Count > 0)
        {
            await _hubContext.Clients.Groups(supervisors).CallQualityAlert(notification);
        }
    }

    // An offer belongs to its queue: only that queue's supervisors hear of it. One with no queue (a call straight to
    // the agent) is heard by the supervisors of the agent's queues.
    private async Task<IReadOnlyList<string>> GetOfferSupervisorGroupsAsync(string queueId, string agentId, string userId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(queueId))
        {
            return [SupervisorQueueGroup(queueId)];
        }

        return await GetSupervisorGroupsAsync([], agentId, userId, cancellationToken);
    }

    // The supervisor groups of the queues an agent's event concerns: the queues it names, and every queue and campaign
    // the agent belongs to or may serve. The agent's queues are read from their profile because an event does not
    // always name them: signing out names none, though the supervisors of the queues just left still need to hear it.
    private async Task<IReadOnlyList<string>> GetSupervisorGroupsAsync(IEnumerable<string> queueIds, string agentId, string userId, CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var queueId in queueIds ?? [])
        {
            if (!string.IsNullOrWhiteSpace(queueId))
            {
                ids.Add(queueId);
            }
        }

        AgentProfile agent = null;

        if (!string.IsNullOrWhiteSpace(agentId))
        {
            agent = await _agentManager.FindByIdAsync(agentId, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(userId))
        {
            agent = await _agentManager.FindByUserIdAsync(userId, cancellationToken);
        }

        if (agent is not null)
        {
            foreach (var queueId in (agent.QueueIds ?? []).Concat(agent.AllowedQueueIds ?? []))
            {
                if (!string.IsNullOrWhiteSpace(queueId))
                {
                    ids.Add(queueId);
                }
            }

            foreach (var campaignId in (agent.CampaignIds ?? []).Concat(agent.AllowedCampaignIds ?? []))
            {
                if (!string.IsNullOrWhiteSpace(campaignId))
                {
                    ids.Add(ContactCenterConstants.CampaignQueue.CreateId(campaignId));
                }
            }
        }

        return ids.Select(SupervisorQueueGroup).ToArray();
    }

    private string SupervisorQueueGroup(string queueId)
    {
        return TenantSignalRGroupName.ForGroup(_tenantName, ContactCenterHub.SupervisorQueueGroup(queueId));
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
