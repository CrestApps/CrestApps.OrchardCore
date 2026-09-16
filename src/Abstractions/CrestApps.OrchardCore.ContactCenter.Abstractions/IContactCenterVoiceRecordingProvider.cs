using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Executes provider-confirmed recording state changes.
/// </summary>
public interface IContactCenterVoiceRecordingProvider
{
    /// <summary>
    /// Changes the recording state of a live provider call.
    /// </summary>
    /// <param name="request">The recording request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> SetRecordingStateAsync(
        ContactCenterVoiceRecordingRequest request,
        CancellationToken cancellationToken = default);
}
