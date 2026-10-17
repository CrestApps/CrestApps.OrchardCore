namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// What a telephony provider reports about a clip that was uploaded to its media storage earlier.
/// </summary>
public enum VoiceMediaAvailability
{
    /// <summary>
    /// The provider could not say whether it still holds the clip, for example because the request failed or the
    /// provider is not configured. Callers keep the reference they have rather than uploading the clip again.
    /// </summary>
    Unknown,

    /// <summary>
    /// The provider confirmed it still holds the clip under the reference.
    /// </summary>
    Available,

    /// <summary>
    /// The provider confirmed it no longer holds the clip: it was deleted, expired, or belongs to another account.
    /// </summary>
    Missing,
}
