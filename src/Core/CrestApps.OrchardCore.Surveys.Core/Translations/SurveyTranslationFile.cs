namespace CrestApps.OrchardCore.Surveys.Core.Translations;

/// <summary>
/// The JSON file a survey translation is exported to and imported from.
/// </summary>
public sealed class SurveyTranslationFile
{
    /// <summary>
    /// Gets or sets the technical name of the survey.
    /// </summary>
    public string Survey { get; set; }

    /// <summary>
    /// Gets or sets the culture of the translation.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets the texts: each with its key, the original text and the translation.
    /// </summary>
    public List<SurveyTranslationFileEntry> Texts { get; set; } = [];
}
