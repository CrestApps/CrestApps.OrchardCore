using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Resolves a provider's effective audio mode from its executable capabilities and configuration.
/// </summary>
public static class TelephonyAudioModeResolver
{
    /// <summary>
    /// Resolves the effective audio mode.
    /// </summary>
    /// <param name="capabilities">The provider's executable audio capabilities.</param>
    /// <param name="configuredMode">The provider-selected mode when multiple modes are supported.</param>
    /// <param name="browserMediaAdapterName">The registered browser media adapter name.</param>
    /// <returns>The effective audio mode, or <see cref="TelephonyAudioMode.None"/> when the configuration is not executable.</returns>
    public static TelephonyAudioMode Resolve(
        TelephonyAudioCapabilities capabilities,
        TelephonyAudioMode configuredMode,
        string browserMediaAdapterName)
    {
        var supportsBrowser = capabilities.HasFlag(TelephonyAudioCapabilities.Browser) &&
            !string.IsNullOrWhiteSpace(browserMediaAdapterName);
        var supportsExternalDevice = capabilities.HasFlag(TelephonyAudioCapabilities.ExternalDevice);

        if (supportsBrowser && supportsExternalDevice)
        {
            return configuredMode switch
            {
                TelephonyAudioMode.Browser => TelephonyAudioMode.Browser,
                TelephonyAudioMode.ExternalDevice => TelephonyAudioMode.ExternalDevice,
                _ => TelephonyAudioMode.None,
            };
        }

        if (supportsBrowser)
        {
            return TelephonyAudioMode.Browser;
        }

        if (supportsExternalDevice)
        {
            return TelephonyAudioMode.ExternalDevice;
        }

        return TelephonyAudioMode.None;
    }
}
