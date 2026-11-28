namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// How respondents move through a survey.
/// </summary>
public sealed class SurveyNavigationSettings
{
    /// <summary>
    /// Gets or sets whether respondents may go back.
    /// </summary>
    public bool AllowBack { get; set; } = true;

    /// <summary>
    /// Gets or sets whether respondents must go forward page by page without going back.
    /// </summary>
    public bool Sequential { get; set; }

    /// <summary>
    /// Gets or sets whether a page with one single-choice question moves on when it is answered.
    /// </summary>
    public bool AutoAdvance { get; set; }

    /// <summary>
    /// Gets or sets whether respondents may leave and come back to an unfinished response.
    /// </summary>
    public bool AllowResume { get; set; } = true;

    /// <summary>
    /// Gets or sets whether respondents may change a completed response.
    /// </summary>
    public bool AllowEditAfterSubmit { get; set; }

    /// <summary>
    /// Gets or sets whether respondents confirm before they finish.
    /// </summary>
    public bool ConfirmBeforeSubmit { get; set; }
}
