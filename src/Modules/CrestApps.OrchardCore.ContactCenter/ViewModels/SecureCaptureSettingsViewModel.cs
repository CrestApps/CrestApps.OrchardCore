namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// View model for the Contact Center hosted secure data capture settings page.
/// </summary>
public class SecureCaptureSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether agents may initiate secure data capture for this tenant.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the lifetime, in seconds, of the one-time customer capture link before it expires.
    /// </summary>
    public int LinkTimeToLiveSeconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether starting a capture pauses recording for the duration of the capture.
    /// </summary>
    public bool PauseRecordingDuringCapture { get; set; }
}
