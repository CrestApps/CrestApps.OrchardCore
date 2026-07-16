namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Identifies the effective live audio delivery mode used by the soft phone.
/// </summary>
public enum TelephonyAudioMode
{
    /// <summary>
    /// No executable agent audio path is available.
    /// </summary>
    None,

    /// <summary>
    /// The soft phone captures microphone audio and plays remote audio in the browser.
    /// </summary>
    Browser,

    /// <summary>
    /// Audio is handled by an external device or provider-owned application.
    /// </summary>
    ExternalDevice,
}
