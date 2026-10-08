namespace CrestApps.OrchardCore.Telnyx.Models;

/// <summary>
/// The Telnyx noise suppression engine applied to an agent's calls, which cleans background sound out of the call audio
/// on Telnyx's side before the other party hears it.
/// </summary>
/// <remarks>
/// Noise suppression is a Telnyx beta feature, and Telnyx bills it for each direction it cleans.
/// </remarks>
public enum TelnyxNoiseSuppressionEngine
{
    /// <summary>
    /// No noise suppression. The default: it is billed, and a quiet room does not need it.
    /// </summary>
    Off = 0,

    /// <summary>
    /// Krisp, which removes other people talking nearby as well as steady noise. The engine for a busy call center floor.
    /// </summary>
    Krisp = 1,

    /// <summary>
    /// DeepFilterNet, an open-source engine tuned for telephony and WebRTC audio.
    /// </summary>
    DeepFilterNet = 2,

    /// <summary>
    /// Telnyx's general-purpose denoiser, for steady background noise such as fans and traffic.
    /// </summary>
    Denoiser = 3,

    /// <summary>
    /// ai-coustics, an engine that prepares speech for speech recognition rather than for a listener.
    /// </summary>
    AiCoustics = 4,
}
