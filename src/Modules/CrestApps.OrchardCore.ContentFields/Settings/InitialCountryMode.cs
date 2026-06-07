namespace CrestApps.OrchardCore.ContentFields.Settings;

/// <summary>
/// Determines how the initial country flag is selected when the phone field is empty.
/// </summary>
public enum InitialCountryMode
{
    /// <summary>
    /// Shows the globe icon without pre-selecting any country.
    /// This is the default behavior.
    /// </summary>
    Globe,

    /// <summary>
    /// Resolves the country from the current culture's region information.
    /// </summary>
    CurrentCulture,

    /// <summary>
    /// Uses a specific country code configured in the field settings.
    /// </summary>
    Specific,
}
