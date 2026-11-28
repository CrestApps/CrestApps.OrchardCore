using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// Shows a survey in a content item or a widget: inline, as a button or as a link to its public link.
/// </summary>
public sealed class SurveyPart : ContentPart
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the id of the collector responses come through, or <see langword="null"/> for the default link.
    /// </summary>
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets how the survey shows.
    /// </summary>
    public SurveyEmbedDisplay Display { get; set; }

    /// <summary>
    /// Gets or sets the text of the button or link, or <see langword="null"/> for the survey title.
    /// </summary>
    public string Text { get; set; }
}
