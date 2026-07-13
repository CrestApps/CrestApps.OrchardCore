using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Defines the optional live-media operations implemented by voice providers that advertise
/// <see cref="ContactCenterVoiceProviderCapabilities.BidirectionalMedia"/>.
/// </summary>
public interface IContactCenterVoiceMediaProvider
{
    /// <summary>
    /// Gets the technical name of the corresponding Contact Center voice provider.
    /// </summary>
    string TechnicalName { get; }

    /// <summary>
    /// Opens a bidirectional media session for an existing provider call.
    /// </summary>
    /// <param name="request">The media-session request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The opened live media session.</returns>
    Task<IContactCenterVoiceMediaSession> OpenSessionAsync(
        ContactCenterVoiceMediaSessionRequest request,
        CancellationToken cancellationToken = default);
}
