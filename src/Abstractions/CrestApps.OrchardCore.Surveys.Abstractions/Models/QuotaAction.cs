namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// What happens once a quota is full.
/// </summary>
public enum QuotaAction
{
    /// <summary>
    /// Matching responses end with the quota's ending, screened out.
    /// </summary>
    ScreenOut,

    /// <summary>
    /// The survey closes.
    /// </summary>
    CloseSurvey,

    /// <summary>
    /// Nothing; the quota only counts.
    /// </summary>
    None,
}
