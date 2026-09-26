using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.SignalR.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.ContactCenter.Hubs;

/// <summary>
/// The supervisor's own engagement, driven from their soft phone: changing between listening, whispering and barging,
/// and stopping. Starting one, and every other intervention, is made from the live dashboard.
/// </summary>
public sealed partial class ContactCenterHub
{
    /// <summary>
    /// Changes the calling supervisor's engagement on a call to another mode, on the same leg.
    /// </summary>
    /// <param name="interactionId">The interaction the supervisor is engaged on.</param>
    /// <param name="mode">The new mode.</param>
    /// <returns>The result.</returns>
    public async Task<SupervisorEngagementResult> SwitchMonitorMode(string interactionId, MonitorMode mode)
    {
        var userId = EnsureUserId();
        SupervisorEngagementResult result = null;

        await _scopeExecutor.ExecuteAsync<ContactCenterSupervisionHubScopeContext>(async services =>
        {
            result = await EnsureSupervisedAsync(services, interactionId)
                ?? await services.MonitoringService.SwitchModeAsync(interactionId, userId, Context.User, mode, HubConnectionWork.MustComplete);
        });

        return result;
    }

    /// <summary>
    /// Stops the calling supervisor's engagement on a call. The call goes on as it was for the agent and the customer.
    /// </summary>
    /// <param name="interactionId">The interaction the supervisor is engaged on.</param>
    /// <returns>The result.</returns>
    public async Task<SupervisorEngagementResult> StopMonitoring(string interactionId)
    {
        var userId = EnsureUserId();
        SupervisorEngagementResult result = null;

        await _scopeExecutor.ExecuteAsync<ContactCenterSupervisionHubScopeContext>(async services =>
        {
            result = await EnsureSupervisedAsync(services, interactionId);

            if (result is not null)
            {
                return;
            }

            var session = services.CallSessionManager is null
                ? null
                : await services.CallSessionManager.FindByInteractionIdAsync(interactionId, HubConnectionWork.MustComplete);
            var engagement = session?.ActiveMonitorSessions.FirstOrDefault(monitorSession =>
                string.Equals(monitorSession.SupervisorUserId, userId, StringComparison.Ordinal));

            result = engagement is null
                ? SupervisorEngagementResult.Success()
                : await services.MonitoringService.StopEngagementAsync(interactionId, userId, Context.User, engagement.Mode, HubConnectionWork.MustComplete);
        });

        return result;
    }

    // Null when the caller may act on their engagement on this interaction; otherwise the refusal.
    private async Task<SupervisorEngagementResult> EnsureSupervisedAsync(ContactCenterSupervisionHubScopeContext services, string interactionId)
    {
        var principal = Context.GetHttpContext()?.User;

        if (principal is null || !await services.AuthorizationService.AuthorizeAsync(principal, ContactCenterPermissions.MonitorContactCenter))
        {
            throw new HubException($"The current user is not authorized for '{ContactCenterPermissions.MonitorContactCenter.Name}'.");
        }

        if (services.MonitoringService is null)
        {
            return SupervisorEngagementResult.Failure("Supervisor monitoring is not available.");
        }

        var interaction = string.IsNullOrEmpty(interactionId)
            ? null
            : await services.InteractionManager.FindByIdAsync(interactionId, HubConnectionWork.MustComplete);

        if (interaction is null ||
            !await services.SupervisorQueueAuthorizationService.IsAuthorizedAsync(principal, Context.UserIdentifier, interaction.QueueId, HubConnectionWork.MustComplete))
        {
            return SupervisorEngagementResult.Failure("The call could not be found.");
        }

        return null;
    }
}
