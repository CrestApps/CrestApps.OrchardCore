namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Where a page post goes.
/// </summary>
public enum SurveyPageDirection
{
    /// <summary>
    /// To the next page (validates the page).
    /// </summary>
    Next,

    /// <summary>
    /// To the previous page (saves without validating).
    /// </summary>
    Back,

    /// <summary>
    /// Finish (Next on the last page).
    /// </summary>
    Finish,
}
