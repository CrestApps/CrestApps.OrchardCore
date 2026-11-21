namespace CrestApps.OrchardCore.Learning.Surveys.Models;

/// <summary>
/// The settings of the course survey part on a content type.
/// </summary>
public sealed class CourseSurveyPartSettings
{
    /// <summary>
    /// Gets or sets whether items of the type are one-question polls (Choice) rather than surveys.
    /// </summary>
    public bool SingleQuestion { get; set; }
}
