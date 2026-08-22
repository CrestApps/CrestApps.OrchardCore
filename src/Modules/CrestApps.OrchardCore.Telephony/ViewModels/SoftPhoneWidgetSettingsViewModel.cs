using Microsoft.AspNetCore.Mvc.Rendering;

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
    /// Gets or sets the accent color, as a CSS color value, used by the soft phone widget.
    /// </summary>
    public string AccentColor { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of calls shown in the recent-calls history.
    /// </summary>
    public int RecentCallsCount { get; set; }

    /// <summary>
    /// Gets or sets the default ISO 3166-1 alpha-2 country code the soft phone's phone number input
    /// selects initially. When empty, the country is derived from the current request culture.
    /// </summary>
    public string DefaultCountryCode { get; set; }

    /// <summary>
    /// Gets or sets the list of selectable countries shown in the default-country editor.
    /// </summary>
    public IList<SelectListItem> Countries { get; set; }
}
