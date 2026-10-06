using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Lists the calls that were recorded, so they can be found and played on the call recordings page. Voice providers
/// call it when a recording is saved and again once it lands in the media store. It is only registered while the
/// Contact Center Call Recording feature is enabled, so callers resolve it optionally.
/// </summary>
public interface ICallRecordingCatalog
{
    /// <summary>
    /// Lists a recording a provider just saved. Registering the same provider recording again changes nothing.
    /// Written on the ambient session, so it commits with the caller's unit of work.
    /// </summary>
    /// <param name="registration">What the provider knows about the recording.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The catalog entry.</returns>
    Task<CallRecording> RegisterAsync(CallRecordingRegistration registration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a recording as stored in the media store, which makes it playable.
    /// </summary>
    /// <param name="providerRecordingId">The provider's identifier of the recording.</param>
    /// <param name="storageReference">The reference it was stored under.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the recording is listed.</returns>
    Task<bool> MarkStoredAsync(string providerRecordingId, string storageReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a recording that belongs to no interaction was erased, or was never listed. Recordings of an
    /// interaction are guarded by the interaction's own erasure instead.
    /// </summary>
    /// <param name="providerRecordingId">The provider's identifier of the recording.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the recording must not be stored.</returns>
    Task<bool> IsErasedAsync(string providerRecordingId, CancellationToken cancellationToken = default);
}
