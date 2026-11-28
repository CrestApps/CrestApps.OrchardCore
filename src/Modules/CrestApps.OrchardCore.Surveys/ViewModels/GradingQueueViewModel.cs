using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The grading queue page.
/// </summary>
public class GradingQueueViewModel
{
    /// <summary>
    /// Gets or sets the survey the queue is narrowed to, or <see langword="null"/>.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the title of that survey.
    /// </summary>
    public string SurveyTitle { get; set; }

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    public AdminListContext List { get; set; }
}
