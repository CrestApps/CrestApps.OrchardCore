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
}
