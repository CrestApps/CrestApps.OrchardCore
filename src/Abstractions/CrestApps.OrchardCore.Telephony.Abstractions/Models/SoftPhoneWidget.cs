namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents the floating soft phone widget rendered through Orchard Core display management.
/// </summary>
public sealed class SoftPhoneWidget
{
    /// <summary>
    /// Gets or sets the accent color used by the widget.
    /// </summary>
    public string AccentColor { get; set; } = "#2f6fed";

    /// <summary>
    /// Gets or sets the maximum number of recent calls displayed in the history tab.
    /// </summary>
    public int RecentCallsCount { get; set; } = 30;

    /// <summary>
    /// Gets or sets the telephony operations supported by the active provider.
    /// </summary>
    public TelephonyCapabilities Capabilities { get; set; }

    /// <summary>
    /// Gets or sets the provider's executable audio delivery capabilities.
    /// </summary>
    public TelephonyAudioCapabilities AudioCapabilities { get; set; }

    /// <summary>
    /// Gets or sets the effective audio delivery mode.
    /// </summary>
    public TelephonyAudioMode AudioMode { get; set; }

    /// <summary>
    /// Gets or sets the browser media adapter name when browser audio is active.
    /// </summary>
    public string BrowserMediaAdapterName { get; set; }
}
