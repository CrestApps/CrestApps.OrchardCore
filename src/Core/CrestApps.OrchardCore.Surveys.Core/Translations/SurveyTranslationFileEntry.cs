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
    /// Gets or sets the original text the translation was made from. On import, an entry whose original no longer
    /// matches the survey's text is left out, so a file translated before an item was reworded never writes the old meaning.
    /// </summary>
    public string Original { get; set; }

    /// <summary>
    /// Gets or sets the translation; empty when not translated yet.
    /// </summary>
    public string Translation { get; set; }
}
