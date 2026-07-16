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
}
