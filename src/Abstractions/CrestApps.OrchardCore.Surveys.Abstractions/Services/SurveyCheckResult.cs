using CrestApps.OrchardCore.Surveys.Controls;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The result of checking one answer.
/// </summary>
public sealed class SurveyCheckResult : SurveyResponseResult
{
    /// <summary>
    /// Gets or sets the feedback the review settings allow during the response, or <see langword="null"/>.
    /// </summary>
    public SurveyAnswerFeedback Feedback { get; set; }

    /// <summary>
    /// Gets or sets the points, when the review settings show the score.
    /// </summary>
    public decimal? Points { get; set; }

    /// <summary>
    /// Gets or sets the errors of the answer.
    /// </summary>
    public List<string> Errors { get; set; } = [];
}
