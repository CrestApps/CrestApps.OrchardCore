using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Surveys.Models;

/// <summary>
/// A learner's answers to a survey: one record per learner and survey, replaced in place when the survey lets the
/// learner change their answers.
/// </summary>
public sealed class SurveyResponse : CatalogItem
{
    /// <summary>
    /// Gets or sets the learner's enrollment.
    /// </summary>
    public string EnrollmentId { get; set; }

    /// <summary>
    /// Gets or sets the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the course of the survey.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the survey course item.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the title of the survey when it was answered.
    /// </summary>
    public string SurveyTitle { get; set; }

    /// <summary>
    /// Gets or sets the answers by question id: the chosen choices, the rating, or the written answer.
    /// </summary>
    public Dictionary<string, List<string>> Answers { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets when the learner first answered, in UTC.
    /// </summary>
    public DateTime SubmittedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the learner last changed their answers, in UTC.
    /// </summary>
    public DateTime? UpdatedUtc { get; set; }
}
