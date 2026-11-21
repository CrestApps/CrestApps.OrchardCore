namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// One choice of a choice-based question, or one column of a matrix.
/// </summary>
public sealed class SurveyChoice
{
    /// <summary>
    /// Gets or sets the stable id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the export code, such as 1 or very_satisfied.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the text; supports piping.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the media path of an image choice.
    /// </summary>
    public string ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets whether choosing it shows a text box for another answer.
    /// </summary>
    public bool IsOther { get; set; }

    /// <summary>
    /// Gets or sets whether choosing it clears the other choices, such as None of the above.
    /// </summary>
    public bool IsExclusive { get; set; }

    /// <summary>
    /// Gets or sets whether it keeps its place when the choices shuffle.
    /// </summary>
    public bool Pinned { get; set; }

    /// <summary>
    /// Gets or sets whether it is hidden from new respondents but kept for results.
    /// </summary>
    public bool Retired { get; set; }

    /// <summary>
    /// Gets or sets when the choice shows.
    /// </summary>
    public SurveyVisibility Visibility { get; set; } = new();
}
