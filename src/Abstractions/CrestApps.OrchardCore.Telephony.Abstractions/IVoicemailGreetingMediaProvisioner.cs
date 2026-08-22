namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Provisions an agent's recorded or uploaded voicemail greeting into the telephony provider's own media storage,
/// so the provider can play it back to callers without the platform hosting a publicly reachable URL of its own.
/// Only providers that host media (for example Telnyx Media Storage) register an implementation; when none is
/// registered, audio greetings are unavailable and voicemail falls back to a spoken text greeting.
/// </summary>
public interface IVoicemailGreetingMediaProvisioner
{
    /// <summary>
    /// Uploads the greeting audio to the provider's media storage and returns the opaque provider reference (for
    /// Telnyx, the Media Storage <c>media_name</c>) used later to play it back. Returns <see langword="null"/> when
    /// the upload could not be completed.
    /// </summary>
    /// <param name="audio">The greeting audio stream.</param>
    /// <param name="contentType">The audio content type (for example <c>audio/mpeg</c> or <c>audio/wav</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<string> UploadAsync(Stream audio, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a previously uploaded greeting from the provider's media storage. Deletion is best-effort and
    /// idempotent: a reference that is already absent is treated as deleted.
    /// </summary>
    /// <param name="mediaReference">The provider reference returned from <see cref="UploadAsync"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task DeleteAsync(string mediaReference, CancellationToken cancellationToken = default);
}
