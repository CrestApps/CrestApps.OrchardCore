namespace CrestApps.OrchardCore.ContentFields.Settings;

/// <summary>
/// Stores the settings for a <see cref="Fields.PhoneField"/>.
/// </summary>
public sealed class PhoneFieldSettings
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
    /// Gets or sets how the initial country flag is selected when no
    /// country has been stored for the field value.
    /// Defaults to <see cref="InitialCountryMode.Globe"/>.
    /// </summary>
    public InitialCountryMode InitialCountryMode { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code used when
    /// <see cref="InitialCountryMode"/> is <see cref="Settings.InitialCountryMode.Specific"/>
    /// (e.g., "US").
    /// </summary>
    public string SpecificCountryCode { get; set; }
}
