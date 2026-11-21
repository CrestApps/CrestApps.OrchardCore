namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// A link to the owner of a survey.
/// </summary>
public sealed class SurveyOwnerLink
{
    /// <summary>
    /// Gets or sets the text of the link, such as the title of the course item.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the local URL of the owner's page.
    /// </summary>
    public string Url { get; set; }
}
