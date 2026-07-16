using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Describes how a telephony provider delivers live call audio to the agent.
/// </summary>
public interface ITelephonyAudioProvider
{
    /// <summary>
    /// Gets the audio delivery modes supported by the provider.
    /// </summary>
    TelephonyAudioCapabilities AudioCapabilities { get; }

    /// <summary>
    /// Gets the provider-configured audio delivery mode when more than one mode is supported.
    /// </summary>
    TelephonyAudioMode ConfiguredAudioMode { get; }

    /// <summary>
    /// Gets the browser media adapter name registered by the provider when browser audio is supported.
    /// </summary>
    string BrowserMediaAdapterName { get; }
}
