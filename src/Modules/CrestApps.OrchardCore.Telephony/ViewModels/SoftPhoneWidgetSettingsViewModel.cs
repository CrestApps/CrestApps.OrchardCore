namespace CrestApps.OrchardCore.Telephony.ViewModels;

/// <summary>
/// View model for editing the soft phone widget settings.
/// </summary>
public class SoftPhoneWidgetSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the floating soft phone widget is shown on the admin dashboard.
    /// </summary>
    public bool DisplayOnAdmin { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the floating soft phone widget is shown on the front end.
    /// </summary>
    public bool DisplayOnFrontend { get; set; }

    /// <summary>
    /// Gets or sets the accent color, as a CSS color value, used by the soft phone widget.
    /// </summary>
    public string AccentColor { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of calls shown in the recent-calls history.
    /// </summary>
    public int RecentCallsCount { get; set; }
}
