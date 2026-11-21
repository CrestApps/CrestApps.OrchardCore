namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// When a respondent looks at a response.
/// </summary>
public enum SurveyReviewMoment
{
    /// <summary>
    /// The response is in progress.
    /// </summary>
    During,

    /// <summary>
    /// Within two minutes of finishing.
    /// </summary>
    ImmediatelyAfter,

    /// <summary>
    /// Later, while the survey is still open.
    /// </summary>
    LaterWhileOpen,

    /// <summary>
    /// After the survey closed.
    /// </summary>
    AfterClose,
}
