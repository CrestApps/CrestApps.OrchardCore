namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// Where a response stands.
/// </summary>
public enum SurveyResponseStatus
{
    /// <summary>
    /// The respondent is answering.
    /// </summary>
    InProgress,

    /// <summary>
    /// The respondent finished.
    /// </summary>
    Completed,

    /// <summary>
    /// The respondent was screened out.
    /// </summary>
    ScreenedOut,

    /// <summary>
    /// The response was left unfinished for too long.
    /// </summary>
    Abandoned,

    /// <summary>
    /// Staff voided the response.
    /// </summary>
    Voided,
}
