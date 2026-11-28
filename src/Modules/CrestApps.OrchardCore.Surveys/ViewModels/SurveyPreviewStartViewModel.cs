namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The admin page that posts the start of a preview, so opening the Preview link alone creates no test response.
/// </summary>
public class SurveyPreviewStartViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the survey title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets which version runs: <c>draft</c> or <c>published</c>.
    /// </summary>
    public string Version { get; set; }
}
