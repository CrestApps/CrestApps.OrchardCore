using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// One row of the grading queue: a response that needs a grader, with its survey and respondent.
/// </summary>
public class GradingQueueItem
{
    /// <summary>
    /// Gets or sets the response.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets the survey title.
    /// </summary>
    public string SurveyTitle { get; set; }

    /// <summary>
    /// Gets or sets the respondent's name, or <see langword="null"/> for an anonymous response.
    /// </summary>
    public string RespondentName { get; set; }

    /// <summary>
    /// Gets or sets how many questions need a grader.
    /// </summary>
    public int PendingQuestions { get; set; }
}
