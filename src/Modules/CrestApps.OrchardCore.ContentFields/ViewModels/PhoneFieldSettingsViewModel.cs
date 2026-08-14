using CrestApps.OrchardCore.ContentFields.Settings;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContentFields.ViewModels;

/// <summary>
/// View model used when editing <see cref="PhoneFieldSettings"/>.
/// </summary>
public class PhoneFieldSettingsViewModel
{
    /// <summary>
    /// Gets or sets the hint text displayed below the field.
    /// </summary>
    public string Hint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the field is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the initial country mode.
    /// </summary>
    public InitialCountryMode InitialCountryMode { get; set; }

    /// <summary>
    /// Gets or sets the specific country code when
    /// <see cref="InitialCountryMode"/> is <see cref="Settings.InitialCountryMode.Specific"/>.
    /// </summary>
    public string SpecificCountryCode { get; set; }

    /// <summary>
    /// Gets or sets the available initial country mode choices.
    /// </summary>
    public List<SelectListItem> InitialCountryModeOptions { get; set; }

    /// <summary>
    /// Gets or sets the available country choices for the specific mode.
    /// </summary>
    public List<SelectListItem> CountryOptions { get; set; }
}
