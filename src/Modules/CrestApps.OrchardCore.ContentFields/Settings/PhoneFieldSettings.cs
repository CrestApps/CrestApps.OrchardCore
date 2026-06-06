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
    /// Gets or sets the default ISO 3166-1 alpha-2 country code used
    /// when no country has been selected yet (e.g., "US").
    /// </summary>
    public string DefaultCountryCode { get; set; }
}
