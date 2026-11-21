namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// When respondents may see the totals of all responses.
/// </summary>
public enum ResultsVisibility
{
    /// <summary>
    /// Never.
    /// </summary>
    Never,

    /// <summary>
    /// After they answer.
    /// </summary>
    AfterAnswer,

    /// <summary>
    /// After the survey closes.
    /// </summary>
    AfterClose,

    /// <summary>
    /// Always, even before they answer.
    /// </summary>
    Always,
}
