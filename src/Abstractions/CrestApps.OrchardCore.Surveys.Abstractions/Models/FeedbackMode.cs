namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// When answers are checked.
/// </summary>
public enum FeedbackMode
{
    /// <summary>
    /// When the response is completed.
    /// </summary>
    Deferred,

    /// <summary>
    /// One question at a time, when the respondent checks it; the answer is then locked.
    /// </summary>
    Immediate,
}
