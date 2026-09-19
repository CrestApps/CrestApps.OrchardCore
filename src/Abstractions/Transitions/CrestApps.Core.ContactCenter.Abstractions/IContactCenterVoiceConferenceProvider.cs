using CrestApps.Core.ContactCenter.Models;

namespace CrestApps.Core.ContactCenter;

/// <summary>
/// Executes provider-confirmed conference operations.
/// </summary>
public interface IContactCenterVoiceConferenceProvider
{
    /// <summary>
    /// Creates or updates a conference for live provider calls.
    /// </summary>
    /// <param name="request">The conference request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> ConferenceAsync(
        ContactCenterVoiceConferenceRequest request,
        CancellationToken cancellationToken = default);
}
