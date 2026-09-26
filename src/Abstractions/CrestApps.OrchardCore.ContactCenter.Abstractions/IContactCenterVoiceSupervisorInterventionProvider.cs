using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// The supervisor interventions a monitoring provider can make on an engagement already started, beyond starting and
/// stopping it: changing how the supervisor is heard without ringing them again, and taking the call over from the
/// agent.
/// </summary>
public interface IContactCenterVoiceSupervisorInterventionProvider
{
    /// <summary>
    /// Changes an engaged supervisor to another mode on the same leg.
    /// </summary>
    /// <param name="request">The engagement, with <see cref="ContactCenterVoiceMonitoringRequest.SupervisorLegId"/> and the new <see cref="ContactCenterVoiceMonitoringRequest.Mode"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> SwitchModeAsync(
        ContactCenterVoiceMonitoringRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the engaged supervisor a full party of the call and releases the agent's leg, leaving the customer with
    /// the supervisor.
    /// </summary>
    /// <param name="request">The engagement, with <see cref="ContactCenterVoiceMonitoringRequest.SupervisorLegId"/> and <see cref="ContactCenterVoiceMonitoringRequest.AgentLegId"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> TakeOverAsync(
        ContactCenterVoiceMonitoringRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hangs up a supervisor's leg that outlived the call it was listening to. Releasing a leg that is already gone is
    /// not an error.
    /// </summary>
    /// <param name="supervisorLegId">The supervisor's leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ReleaseSupervisorLegAsync(string supervisorLegId, CancellationToken cancellationToken = default);
}
