using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Executes provider-confirmed supervisor monitoring engagements.
/// </summary>
public interface IContactCenterVoiceMonitoringProvider
{
    /// <summary>
    /// Starts the requested supervisor engagement on a live provider call.
    /// </summary>
    /// <param name="request">The monitoring request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> EngageAsync(
        ContactCenterVoiceMonitoringRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a previously started supervisor engagement, releasing only the supervisor-owned media legs without
    /// affecting the underlying customer-to-agent call. It is idempotent: stopping an engagement whose resources
    /// are already gone succeeds.
    /// </summary>
    /// <param name="request">The monitoring request identifying the engagement to stop (interaction, supervisor, and mode).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> StopAsync(
        ContactCenterVoiceMonitoringRequest request,
        CancellationToken cancellationToken = default);
}
