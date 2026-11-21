namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// One text of the translations page.
/// </summary>
public class SurveyTranslationEntryViewModel
{
    /// <summary>
    /// Gets or sets the stable key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the kind of item: survey, page, question, choice, row or ending.
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Gets or sets the label of the item.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the original text.
    /// </summary>
    public string Original { get; set; }

    /// <summary>
    /// Gets or sets the translation.
    /// </summary>
    public string Translation { get; set; }

    /// <summary>
    /// Gets or sets whether the text is HTML.
    /// </summary>
    public bool IsHtml { get; set; }
}
