using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Decides which live calls a supervisor may act on, the way the live dashboard does: a Contact Center call is named by
/// its interaction and is the supervisor's when they oversee its queue; an agent's own phone call is named by a
/// <see cref="PhoneCallKey"/> and is the supervisor's when they oversee a queue its agent works. The live dashboard's
/// interventions and the supervision AI tools both ask it, so neither reaches a call outside the supervisor's queues.
/// </summary>
internal sealed class SupervisorCallAccess
{
    private readonly IInteractionManager _interactionManager;
    private readonly ISupervisorQueueAuthorizationService _supervisorQueueAuthorizationService;
    private readonly IContactCenterPhoneCallSupervisionService _phoneCalls;
    private readonly ICallSessionManager _sessions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupervisorCallAccess"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="supervisorQueueAuthorizationService">Decides which queues a supervisor oversees.</param>
    /// <param name="phoneCalls">The supervision of agents' own phone calls.</param>
    /// <param name="sessions">The call sessions, read for a supervisor's engagement on a call.</param>
    public SupervisorCallAccess(
        IInteractionManager interactionManager,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
        IContactCenterPhoneCallSupervisionService phoneCalls,
        ICallSessionManager sessions)
    {
        _interactionManager = interactionManager;
        _supervisorQueueAuthorizationService = supervisorQueueAuthorizationService;
        _phoneCalls = phoneCalls;
        _sessions = sessions;
    }

    /// <summary>
    /// Determines whether a supervisor may act on a live call.
    /// </summary>
    /// <param name="principal">The supervisor.</param>
    /// <param name="supervisorId">The supervisor's user identifier.</param>
    /// <param name="callId">An interaction identifier, or a <see cref="PhoneCallKey"/> for an agent's own phone call.</param>
    /// <param name="cancellationToken">A token to cancel the check.</param>
    /// <returns><see langword="true"/> when the call is in the supervisor's queues.</returns>
    public async Task<bool> IsInScopeAsync(ClaimsPrincipal principal, string supervisorId, string callId, CancellationToken cancellationToken = default)
    {
        if (principal is null || string.IsNullOrEmpty(supervisorId) || string.IsNullOrEmpty(callId))
        {
            return false;
        }

        // A phone call has no interaction: the supervisor may act on it when they oversee a queue its agent works.
        if (PhoneCallKey.IsPhoneCall(callId))
        {
            return await _phoneCalls.IsAuthorizedAsync(principal, supervisorId, callId, cancellationToken);
        }

        var interaction = await _interactionManager.FindByIdAsync(callId, cancellationToken);

        return interaction is not null &&
            await _supervisorQueueAuthorizationService.IsAuthorizedAsync(principal, supervisorId, interaction.QueueId, cancellationToken);
    }

    /// <summary>
    /// Finds the mode a supervisor is engaged in on a Contact Center call.
    /// </summary>
    /// <param name="interactionId">The call's interaction.</param>
    /// <param name="supervisorId">The supervisor's user identifier.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The mode, or <see langword="null"/> when the supervisor is not on the call.</returns>
    public async Task<MonitorMode?> FindEngagementModeAsync(string interactionId, string supervisorId, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.FindByInteractionIdAsync(interactionId, cancellationToken);
        var engagement = session?.ActiveMonitorSessions.FirstOrDefault(monitorSession =>
            string.Equals(monitorSession.SupervisorUserId, supervisorId, StringComparison.Ordinal));

        return engagement?.Mode;
    }
}
