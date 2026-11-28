namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// What a respondent may see of a choice.
/// </summary>
public sealed class SurveyChoiceView
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the export value.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the piped text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the image URL, or <see langword="null"/>.
    /// </summary>
    public string ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets whether choosing it shows a text box.
    /// </summary>
    public bool IsOther { get; set; }

    /// <summary>
    /// Gets or sets whether choosing it clears the others.
    /// </summary>
    public bool IsExclusive { get; set; }
}
