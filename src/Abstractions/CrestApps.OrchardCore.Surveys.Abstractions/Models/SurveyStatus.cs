namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// Where a survey stands.
/// </summary>
public enum SurveyStatus
{
    /// <summary>
    /// Never published; nobody can respond.
    /// </summary>
    Draft,

    /// <summary>
    /// Published and collecting responses (subject to its access settings).
    /// </summary>
    Open,

    /// <summary>
    /// Published but not collecting responses.
    /// </summary>
    Closed,

    /// <summary>
    /// Kept for its results but hidden from the default list.
    /// </summary>
    Archived,
}
