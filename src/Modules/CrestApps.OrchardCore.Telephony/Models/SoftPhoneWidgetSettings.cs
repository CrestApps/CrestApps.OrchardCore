namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Site settings that control where the soft phone widget is displayed.
/// </summary>
public sealed class SoftPhoneWidgetSettings
{
    /// <summary>
    /// The default accent color used by the soft phone widget.
    /// </summary>
    public const string DefaultAccentColor = "#2f6fed";

    /// <summary>
    /// The default maximum number of calls shown in the recent-calls history.
    /// </summary>
    public const int DefaultRecentCallsCount = 30;

    /// <summary>
    /// Gets or sets a value indicating whether the floating soft phone widget is shown on the admin dashboard.
    /// </summary>
    public bool DisplayOnAdmin { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the floating soft phone widget is shown on the front end.
    /// </summary>
    public bool DisplayOnFrontend { get; set; }

    /// <summary>
    /// Gets or sets the accent color, as a CSS color value, used by the soft phone widget.
    /// </summary>
    public string AccentColor { get; set; } = DefaultAccentColor;

    /// <summary>
    /// Gets or sets the maximum number of calls shown in the recent-calls history.
    /// </summary>
    public int RecentCallsCount { get; set; } = DefaultRecentCallsCount;

    /// <summary>
    /// Gets or sets the default ISO 3166-1 alpha-2 country code the soft phone's phone number input
    /// selects initially, so a national number can be normalized to E.164. When empty, the country is
    /// derived from the current request culture.
    /// </summary>
    public string DefaultCountryCode { get; set; }
}
