using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Provisions reusable voice audio (voicemail greetings, hold music, IVR prompts) into the telephony provider's own
/// media storage, so the provider can play the clip back to callers by an opaque reference without the platform
/// hosting a publicly reachable URL of its own. Only providers that host media (for example Telnyx Media Storage)
/// register an implementation; when none is registered, provider-hosted audio is unavailable and callers fall back
/// to spoken (text-to-speech) prompts. The provider's copy is a cache: the platform keeps the master copy of every
/// clip in its own media store and uploads it again, through this contract, to any provider that has no copy or
/// reports its copy missing.
/// </summary>
public interface IVoiceMediaProvisioner
{
    /// <summary>
    /// Gets the technical name of the telephony provider that hosts the media (for example <c>Telnyx</c>). Callers
    /// record this alongside the returned reference because a clip is playable only through the provider that stores
    /// it.
    /// </summary>
    string ProviderTechnicalName { get; }

    /// <summary>
    /// Uploads the audio to the provider's media storage and returns the opaque provider reference (for Telnyx, the
    /// Media Storage <c>media_name</c>) used later to play it back. Returns <see langword="null"/> when the upload
    /// could not be completed.
    /// </summary>
    /// <param name="audio">The audio stream.</param>
    /// <param name="contentType">The audio content type (for example <c>audio/mpeg</c> or <c>audio/wav</c>).</param>
    /// <param name="namePrefix">
    /// A short, filename-safe prefix that labels the stored media by purpose (for example <c>cc-vm-greeting</c> or
    /// <c>cc-voice-media</c>). A unique suffix is appended so a re-upload never collides with an earlier one.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<string> UploadAsync(Stream audio, string contentType, string namePrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the provider whether it still holds a previously uploaded clip, so a copy that was deleted or expired at
    /// the provider can be uploaded again from the platform's master copy before a caller is left in silence.
    /// </summary>
    /// <param name="mediaReference">The provider reference returned from <see cref="UploadAsync"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// <see cref="VoiceMediaAvailability.Available"/> or <see cref="VoiceMediaAvailability.Missing"/> when the
    /// provider answered, and <see cref="VoiceMediaAvailability.Unknown"/> when it could not be asked.
    /// </returns>
    Task<VoiceMediaAvailability> GetAvailabilityAsync(string mediaReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a previously uploaded clip from the provider's media storage. Deletion is best-effort and idempotent:
    /// a reference that is already absent is treated as deleted.
    /// </summary>
    /// <param name="mediaReference">The provider reference returned from <see cref="UploadAsync"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task DeleteAsync(string mediaReference, CancellationToken cancellationToken = default);
}
