namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Identifies the live audio delivery modes supported by a telephony provider.
/// </summary>
[Flags]
public enum TelephonyAudioCapabilities
{
    /// <summary>
    /// The provider does not expose an executable agent audio path.
    /// </summary>
    None = 0,

    /// <summary>
    /// The provider can deliver live audio through a browser media adapter and the agent's microphone.
    /// </summary>
    Browser = 1 << 0,

    /// <summary>
    /// The provider delivers live audio through an external device or provider-owned application.
    /// </summary>
    ExternalDevice = 1 << 1,
}
