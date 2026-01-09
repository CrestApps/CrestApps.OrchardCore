namespace CrestApps.OrchardCore.AI.SmartFields.Settings;

/// <summary>
/// Settings for the AI-powered autocomplete TextField editor.
/// </summary>
public sealed class SmartTextFieldAutocompleteSettings
{
    /// <summary>
    /// Gets or sets the ID of the AI profile to use for generating autocomplete suggestions.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets a hint to display for the field.
    /// </summary>
    public string Hint { get; set; }
}
