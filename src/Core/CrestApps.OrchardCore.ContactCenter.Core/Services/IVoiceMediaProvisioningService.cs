using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Keeps the master copy of every clip the platform owns in its own media store and treats each telephony
/// provider's copy as a cache made from it: a provider that has no copy, or has lost it, is given one the moment it
/// is asked to play the clip. Consumers that need something a provider can play resolve it here instead of reading a
/// stored provider reference, so hold music, menu prompts and greetings keep playing after the provider changes.
/// </summary>
public interface IVoiceMediaProvisioningService
{
    /// <summary>
    /// Gets a value indicating whether audio can be uploaded: a media store keeps master copies, a provider that
    /// hosts media is enabled, or both.
    /// </summary>
    bool CanUpload { get; }

    /// <summary>
    /// Gets the technical name of the provider an upload is sent to, which is the tenant's default telephony
    /// provider when it hosts media and otherwise the first enabled provider that does; <see langword="null"/>
    /// when none is enabled.
    /// </summary>
    string ActiveProviderName { get; }

    /// <summary>
    /// Stores newly uploaded audio as the clip's master copy and gives the active provider a copy. The clip's
    /// storage state is replaced: the copies it held before are returned so the caller can delete them once the
    /// record that owns the clip is saved. A provider that refuses the upload does not fail the store when the
    /// master copy was kept; it is given a copy when the clip is next needed. The caller persists the record.
    /// </summary>
    /// <param name="media">The clip whose audio is being replaced.</param>
    /// <param name="audio">The audio to store. A stream that cannot seek is buffered once, so it can be stored and uploaded.</param>
    /// <param name="contentType">The audio content type (for example <c>audio/mpeg</c>).</param>
    /// <param name="format">The audio format (for example <c>mp3</c>).</param>
    /// <param name="namePrefix">A short, filename-safe prefix that labels the provider's copy by purpose.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What was stored and where. <see cref="VoiceMediaStoreResult.Succeeded"/> is <see langword="false"/> when the audio is held nowhere.</returns>
    Task<VoiceMediaStoreResult> StoreAsync(IVoiceMediaAsset media, Stream audio, string contentType, string format, string namePrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes sure the named provider holds a copy of the clip and returns the reference it plays the clip by. A
    /// provider that has no copy, or reports its copy missing, is given one from the master copy. A copy the provider
    /// confirmed within the window of <see cref="IVoiceMediaAvailabilityCache"/> is trusted without asking again, so
    /// a copy lost at the provider is noticed, and made again, at the first play after that window rather than at
    /// the very next play. A clip with no master copy returns the copy the provider was given when the clip was
    /// uploaded, or nothing when that provider never was. The caller persists the record when the result says the
    /// state changed.
    /// </summary>
    /// <param name="media">The clip.</param>
    /// <param name="providerName">The technical name of the provider that is to play the clip.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The reference to play, and where the clip stands with the provider.</returns>
    Task<VoiceMediaProvisioningResult> EnsureProvisionedAsync(IVoiceMediaAsset media, string providerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves what the named provider is to play for a voice media library entry: loads the entry, makes sure the
    /// provider holds a copy as <see cref="EnsureProvisionedAsync"/> does, and stores the entry when a copy was made.
    /// </summary>
    /// <param name="mediaId">The library entry identifier.</param>
    /// <param name="providerName">The technical name of the provider that is to play the clip.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The entry, the reference to play and where the clip stands with the provider.</returns>
    Task<VoiceMediaResolution> ResolveAsync(string mediaId, string providerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks every enabled provider that hosts media still holds its copy of a library entry, gives a copy from
    /// the master copy to each that does not, and stores the entry when a copy was made.
    /// </summary>
    /// <param name="item">The library entry.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What was done for each provider, or that the entry has no master copy to re-sync from.</returns>
    Task<VoiceMediaResyncResult> ResyncAsync(VoiceMediaItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the clip's master copy and every provider copy. Each deletion is best-effort: one that fails is
    /// logged and the others still run, so a copy a provider will not give up never keeps the record from being
    /// removed.
    /// </summary>
    /// <param name="media">The clip whose copies are deleted.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when every deletion has been attempted.</returns>
    Task DeleteEverywhereAsync(IVoiceMediaAsset media, CancellationToken cancellationToken = default);
}
