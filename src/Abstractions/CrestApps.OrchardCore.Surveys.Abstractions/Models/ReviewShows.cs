namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// What a review shows.
/// </summary>
[Flags]
public enum ReviewShows
{
    /// <summary>
    /// Nothing.
    /// </summary>
    None = 0,

    /// <summary>
    /// The score.
    /// </summary>
    Score = 1,

    /// <summary>
    /// The respondent's own answers.
    /// </summary>
    Responses = 2,

    /// <summary>
    /// The right answers.
    /// </summary>
    RightAnswers = 4,

    /// <summary>
    /// The feedback of each answer.
    /// </summary>
    Feedback = 8,

    /// <summary>
    /// The general feedback of each question.
    /// </summary>
    GeneralFeedback = 16,

    /// <summary>
    /// Everything.
    /// </summary>
    All = Score | Responses | RightAnswers | Feedback | GeneralFeedback,
}
