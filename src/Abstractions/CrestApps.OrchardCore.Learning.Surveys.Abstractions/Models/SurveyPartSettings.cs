namespace CrestApps.OrchardCore.Learning.Surveys.Models;

/// <summary>
/// The settings of the survey part on a content type.
/// </summary>
public sealed class SurveyPartSettings
{
    /// <summary>
    /// Gets or sets whether the survey has one choice question, as a choice (a poll) does.
    /// </summary>
    public bool SingleQuestion { get; set; }
}
