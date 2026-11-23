using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The grading page of a response.
/// </summary>
public class GradeResponseViewModel
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
    /// Gets or sets whether the grader may change given scores (change the total, score again, grade a graded response
    /// again), which every <see cref="Services.ISurveyScoreChangeCheck"/> must allow.
    /// </summary>
    public bool CanChangeScores { get; set; }

    /// <summary>
    /// Gets or sets the respondent's name, or <see langword="null"/> for an anonymous response.
    /// </summary>
    public string RespondentName { get; set; }

    /// <summary>
    /// Gets or sets the label staff see for the response: its number, or on an anonymous survey the start of its id.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets whether the survey is anonymous, so times show the day only.
    /// </summary>
    public bool IsAnonymous { get; set; }

    /// <summary>
    /// Gets or sets the scored questions.
    /// </summary>
    public List<GradeQuestionViewModel> Questions { get; set; } = [];

    /// <summary>
    /// Gets or sets the overall feedback.
    /// </summary>
    public string Feedback { get; set; }

    /// <summary>
    /// Gets or sets the points by question id, as posted.
    /// </summary>
    public Dictionary<string, decimal?> Points { get; set; } = [];

    /// <summary>
    /// Gets or sets the comments by question id, as posted.
    /// </summary>
    public Dictionary<string, string> Comments { get; set; } = [];

    /// <summary>
    /// Gets or sets the ids of the questions graded with AI help, as posted.
    /// </summary>
    public List<string> AIAssisted { get; set; } = [];

    /// <summary>
    /// Gets or sets the new total score of the change score form.
    /// </summary>
    public decimal? NewScore { get; set; }

    /// <summary>
    /// Gets or sets the reason of a score change or a regrade.
    /// </summary>
    public string Reason { get; set; }
}
