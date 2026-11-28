namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The result of starting a response.
/// </summary>
public sealed class SurveyStartResult : SurveyResponseResult
{
    /// <summary>
    /// Gets or sets the resume token issued to a respondent who is not signed in, or <see langword="null"/>. It is shown
    /// once: only its hash is stored.
    /// </summary>
    public string ResumeToken { get; set; }

    /// <summary>
    /// Gets or sets whether an existing response in progress was returned instead of a new one.
    /// </summary>
    public bool Resumed { get; set; }
}
