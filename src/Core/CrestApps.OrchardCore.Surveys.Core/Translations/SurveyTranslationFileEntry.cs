namespace CrestApps.OrchardCore.Surveys.Core.Translations;

/// <summary>
/// One text of a translation file.
/// </summary>
public sealed class SurveyTranslationFileEntry
{
    /// <summary>
    /// Gets or sets the stable key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the original text, for the translator; ignored on import.
    /// </summary>
    public string Original { get; set; }

    /// <summary>
    /// Gets or sets the translation; empty when not translated yet.
    /// </summary>
    public string Translation { get; set; }
}
