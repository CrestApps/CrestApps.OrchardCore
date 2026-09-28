using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.SignalR.Core;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Pushes supervisor engagements to the supervisor's own connections -- their soft phone and their live dashboard --
/// and supervisor messages to the agent's, over the <see cref="ContactCenterHub"/>.
/// </summary>
public sealed class ContactCenterSupervisorEngagementNotifier : ISupervisorEngagementNotifier
{
    private readonly IHubContext<ContactCenterHub, IContactCenterHubClient> _hubContext;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSupervisorEngagementNotifier"/> class.
    /// </summary>
    public ContactCenterSupervisorEngagementNotifier(
        IHubContext<ContactCenterHub, IContactCenterHubClient> hubContext,
        ShellSettings shellSettings)
    {
        _hubContext = hubContext;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public Task NotifyEngagementAsync(SupervisorEngagementNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Only the supervisor's own connections: the token in a request is what their phone answers a leg on.
        return string.IsNullOrEmpty(notification.SupervisorUserId)
            ? Task.CompletedTask
            : _hubContext.Clients.Group(TenantSignalRGroupName.ForUser(_tenantName, notification.SupervisorUserId)).SupervisorEngagementChanged(notification);
    }

    /// <inheritdoc/>
    public Task NotifyAgentMessageAsync(SupervisorMessageNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return string.IsNullOrEmpty(notification.UserId)
            ? Task.CompletedTask
            : _hubContext.Clients.Group(TenantSignalRGroupName.ForUser(_tenantName, notification.UserId)).SupervisorMessage(notification);
    }
}
