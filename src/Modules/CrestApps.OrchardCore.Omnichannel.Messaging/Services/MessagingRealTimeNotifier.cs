using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Hubs;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// The SignalR-backed <see cref="IMessagingRealTimeNotifier"/>. It fans an inbound-message or delivery notification
/// out to the agent who owns the conversation, the queue (department) that owns it, or the triage group when
/// it is unassigned — mirroring the groups the hub joins on connect.
/// </summary>
public sealed class MessagingRealTimeNotifier : IMessagingRealTimeNotifier
{
    private readonly IHubContext<MessagingHub, IMessagingHubClient> _hubContext;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRealTimeNotifier"/> class.
    /// </summary>
    /// <param name="hubContext">The messaging workspace hub context.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    public MessagingRealTimeNotifier(
        IHubContext<MessagingHub, IMessagingHubClient> hubContext,
        ShellSettings shellSettings)
    {
        _hubContext = hubContext;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public Task NewInboundMessageAsync(MessagingInboundNotification notification, CancellationToken cancellationToken = default)
        => Target(notification.AssignedAgentId, notification.OwnerQueueId).NewInboundMessage(notification);

    /// <inheritdoc/>
    public Task MessageDeliveryUpdatedAsync(MessagingDeliveryNotification notification, CancellationToken cancellationToken = default)
        => Target(notification.AssignedAgentId, notification.OwnerQueueId).MessageDeliveryUpdated(notification);

    /// <inheritdoc/>
    public Task FirstResponseBreachedAsync(MessagingFirstResponseBreachNotification notification, CancellationToken cancellationToken = default)
        => Target(notification.AssignedAgentId, notification.OwnerQueueId).FirstResponseBreached(notification);

    /// <inheritdoc/>
    public Task ConversationAssignedAsync(MessagingAssignmentNotification notification, CancellationToken cancellationToken = default)
    {
        // Tell the assigned agent it landed in their inbox, the owning queue so other members drop it, and whoever
        // held it before a transfer so it leaves their inbox too.
        var tasks = new List<Task>();

        if (!string.IsNullOrEmpty(notification.AssignedAgentId))
        {
            tasks.Add(_hubContext.Clients.Group(ForGroup(MessagingHub.AgentGroup(notification.AssignedAgentId))).ConversationAssigned(notification));
        }

        if (!string.IsNullOrEmpty(notification.PreviousAgentId) &&
            !string.Equals(notification.PreviousAgentId, notification.AssignedAgentId, StringComparison.Ordinal))
        {
            tasks.Add(_hubContext.Clients.Group(ForGroup(MessagingHub.AgentGroup(notification.PreviousAgentId))).ConversationAssigned(notification));
        }

        if (!string.IsNullOrEmpty(notification.OwnerQueueId))
        {
            tasks.Add(_hubContext.Clients.Group(ForGroup(MessagingHub.QueueGroup(notification.OwnerQueueId))).ConversationAssigned(notification));
        }

        return Task.WhenAll(tasks);
    }

    private IMessagingHubClient Target(string assignedAgentId, string ownerQueueId)
    {
        if (!string.IsNullOrEmpty(assignedAgentId))
        {
            return _hubContext.Clients.Group(ForGroup(MessagingHub.AgentGroup(assignedAgentId)));
        }

        if (!string.IsNullOrEmpty(ownerQueueId))
        {
            return _hubContext.Clients.Group(ForGroup(MessagingHub.QueueGroup(ownerQueueId)));
        }

        return _hubContext.Clients.Group(ForGroup(MessagingHub.UnassignedGroup));
    }

    private string ForGroup(string groupName) => TenantSignalRGroupName.ForGroup(_tenantName, groupName);
}
