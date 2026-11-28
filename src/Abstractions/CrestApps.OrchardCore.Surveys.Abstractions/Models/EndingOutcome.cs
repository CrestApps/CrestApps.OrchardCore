namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// What an ending makes of a response.
/// </summary>
public enum EndingOutcome
{
    /// <summary>
    /// The response is completed.
    /// </summary>
    Completed,

    /// <summary>
    /// The respondent was screened out (disqualified).
    /// </summary>
    ScreenedOut,
}
