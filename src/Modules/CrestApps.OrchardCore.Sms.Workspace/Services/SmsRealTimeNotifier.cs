using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Sms.Workspace.Hubs;
using CrestApps.OrchardCore.Sms.Workspace.Notifications;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Sms.Workspace.Services;

/// <summary>
/// The SignalR-backed <see cref="ISmsRealTimeNotifier"/>. It fans an inbound-message or delivery notification
/// out to the agent who owns the conversation, the queue (department) that owns it, or the triage group when
/// it is unassigned — mirroring the groups the hub joins on connect.
/// </summary>
public sealed class SmsRealTimeNotifier : ISmsRealTimeNotifier
{
    private readonly IHubContext<SmsPortalHub, ISmsPortalHubClient> _hubContext;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsRealTimeNotifier"/> class.
    /// </summary>
    /// <param name="hubContext">The SMS portal hub context.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    public SmsRealTimeNotifier(
        IHubContext<SmsPortalHub, ISmsPortalHubClient> hubContext,
        ShellSettings shellSettings)
    {
        _hubContext = hubContext;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public Task NewInboundMessageAsync(SmsInboundNotification notification, CancellationToken cancellationToken = default)
        => Target(notification.AssignedAgentId, notification.OwnerQueueId).NewInboundMessage(notification);

    /// <inheritdoc/>
    public Task MessageDeliveryUpdatedAsync(SmsDeliveryNotification notification, CancellationToken cancellationToken = default)
        => _hubContext.Clients.All.MessageDeliveryUpdated(notification);

    /// <inheritdoc/>
    public Task ConversationAssignedAsync(SmsAssignmentNotification notification, CancellationToken cancellationToken = default)
    {
        // Tell the assigned agent it landed in their inbox, and the owning queue so other members drop it.
        var tasks = new List<Task>();

        if (!string.IsNullOrEmpty(notification.AssignedAgentId))
        {
            tasks.Add(_hubContext.Clients.Group(ForGroup(SmsPortalHub.AgentGroup(notification.AssignedAgentId))).ConversationAssigned(notification));
        }

        if (!string.IsNullOrEmpty(notification.OwnerQueueId))
        {
            tasks.Add(_hubContext.Clients.Group(ForGroup(SmsPortalHub.QueueGroup(notification.OwnerQueueId))).ConversationAssigned(notification));
        }

        return Task.WhenAll(tasks);
    }

    private ISmsPortalHubClient Target(string assignedAgentId, string ownerQueueId)
    {
        if (!string.IsNullOrEmpty(assignedAgentId))
        {
            return _hubContext.Clients.Group(ForGroup(SmsPortalHub.AgentGroup(assignedAgentId)));
        }

        if (!string.IsNullOrEmpty(ownerQueueId))
        {
            return _hubContext.Clients.Group(ForGroup(SmsPortalHub.QueueGroup(ownerQueueId)));
        }

        return _hubContext.Clients.Group(ForGroup(SmsPortalHub.UnassignedGroup));
    }

    private string ForGroup(string groupName) => TenantSignalRGroupName.ForGroup(_tenantName, groupName);
}
