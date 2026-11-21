using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// Makes a course item a quiz: it names the survey of the Surveys module the quiz owns, where its questions, pools,
/// time limit, attempts, pass mark and review settings live, and how the course combines the learner's attempts.
/// </summary>
public sealed class QuizPart : ContentPart
{
    /// <summary>
    /// Gets or sets the id of the survey the quiz owns, created on its first save.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets how the scores of the learner's attempts combine into the quiz grade.
    /// </summary>
    public GradingMethod GradingMethod { get; set; }
}
