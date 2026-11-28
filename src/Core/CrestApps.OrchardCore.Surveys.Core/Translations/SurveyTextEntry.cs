namespace CrestApps.OrchardCore.Surveys.Core.Translations;

/// <summary>
/// One translatable text of a survey definition.
/// </summary>
public sealed class SurveyTextEntry
{
    /// <summary>
    /// Gets or sets the stable key (see <see cref="SurveyTextKeys"/>).
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the kind of item: survey, page, question, choice, row or ending.
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Gets or sets a short label of the item for editors, such as the question name.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the original text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets whether the text is HTML.
    /// </summary>
    public bool IsHtml { get; set; }
}
