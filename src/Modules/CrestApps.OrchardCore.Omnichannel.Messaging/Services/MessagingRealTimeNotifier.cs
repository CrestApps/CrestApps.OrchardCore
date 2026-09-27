using CrestApps.Core.Support;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Hubs;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger _logger;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRealTimeNotifier"/> class.
    /// </summary>
    /// <param name="hubContext">The messaging workspace hub context.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    /// <param name="logger">The logger.</param>
    public MessagingRealTimeNotifier(
        IHubContext<MessagingHub, IMessagingHubClient> hubContext,
        ShellSettings shellSettings,
        ILogger<MessagingRealTimeNotifier> logger)
    {
        _hubContext = hubContext;
        _tenantName = shellSettings.Name;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task NewInboundMessageAsync(MessagingInboundNotification notification, CancellationToken cancellationToken = default)
    {
        var group = Target(LogLevel.Information, nameof(IMessagingHubClient.NewInboundMessage), notification.ConversationId, notification.AssignedAgentId, notification.OwnerQueueId);

        return Group(group).NewInboundMessage(notification);
    }

    /// <inheritdoc/>
    public Task MessageDeliveryUpdatedAsync(MessagingDeliveryNotification notification, CancellationToken cancellationToken = default)
    {
        // Every outbound message raises several receipts, so they are only worth reading when chasing one.
        var group = Target(LogLevel.Debug, nameof(IMessagingHubClient.MessageDeliveryUpdated), notification.ConversationId, notification.AssignedAgentId, notification.OwnerQueueId);

        return Group(group).MessageDeliveryUpdated(notification);
    }

    /// <inheritdoc/>
    public Task FirstResponseBreachedAsync(MessagingFirstResponseBreachNotification notification, CancellationToken cancellationToken = default)
    {
        var group = Target(LogLevel.Information, nameof(IMessagingHubClient.FirstResponseBreached), notification.ConversationId, notification.AssignedAgentId, notification.OwnerQueueId);

        return Group(group).FirstResponseBreached(notification);
    }

    /// <inheritdoc/>
    public Task ConversationAssignedAsync(MessagingAssignmentNotification notification, CancellationToken cancellationToken = default)
    {
        // Tell the assigned agent it landed in their inbox, the owning queue so other members drop it, and whoever
        // held it before a transfer so it leaves their inbox too.
        var groups = new List<string>();

        if (!string.IsNullOrEmpty(notification.AssignedAgentId))
        {
            groups.Add(MessagingHub.AgentGroup(notification.AssignedAgentId));
        }

        if (!string.IsNullOrEmpty(notification.PreviousAgentId) &&
            !string.Equals(notification.PreviousAgentId, notification.AssignedAgentId, StringComparison.Ordinal))
        {
            groups.Add(MessagingHub.AgentGroup(notification.PreviousAgentId));
        }

        if (!string.IsNullOrEmpty(notification.OwnerQueueId))
        {
            groups.Add(MessagingHub.QueueGroup(notification.OwnerQueueId));
        }

        if (groups.Count == 0)
        {
            _logger.LogWarning(
                "Messaging notification {Event} for conversation {ConversationId} names no agent, previous agent or queue, so it was sent to nobody.",
                nameof(IMessagingHubClient.ConversationAssigned),
                notification.ConversationId.SanitizeLogValue());

            return Task.CompletedTask;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Sending messaging notification {Event} for conversation {ConversationId} (transfer: {IsTransfer}) to {Groups}.",
                nameof(IMessagingHubClient.ConversationAssigned),
                notification.ConversationId.SanitizeLogValue(),
                notification.IsTransfer,
                string.Join(", ", groups).SanitizeLogValue());
        }

        return Task.WhenAll(groups.Select(group => Group(group).ConversationAssigned(notification)));
    }

    // The single group an event for one conversation goes to: its agent, else its queue, else the triage inbox. The
    // choice is logged, since "who was told" is the first question when somebody says they were not.
    private string Target(LogLevel level, string eventName, string conversationId, string assignedAgentId, string ownerQueueId)
    {
        string group;

        if (!string.IsNullOrEmpty(assignedAgentId))
        {
            group = MessagingHub.AgentGroup(assignedAgentId);
        }
        else if (!string.IsNullOrEmpty(ownerQueueId))
        {
            group = MessagingHub.QueueGroup(ownerQueueId);
        }
        else
        {
            // Nobody owns the conversation, so only the supervisors who can view every conversation hear about it.
            // That is by design for a message no route claimed, and it is also why an agent without that permission
            // is not told: the endpoint needs a route to an agent or a queue for them to be.
            if (_logger.IsEnabled(level))
            {
                _logger.Log(
                    level,
                    "Sending messaging notification {Event} for conversation {ConversationId} to the triage group: it has no agent or queue, so only users who can view all conversations are told.",
                    eventName,
                    conversationId.SanitizeLogValue());
            }

            return MessagingHub.UnassignedGroup;
        }

        if (_logger.IsEnabled(level))
        {
            _logger.Log(
                level,
                "Sending messaging notification {Event} for conversation {ConversationId} to {Groups}.",
                eventName,
                conversationId.SanitizeLogValue(),
                group.SanitizeLogValue());
        }

        return group;
    }

    private IMessagingHubClient Group(string groupName)
        => _hubContext.Clients.Group(TenantSignalRGroupName.ForGroup(_tenantName, groupName));
}
