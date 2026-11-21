using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Surveys.Models;

/// <summary>
/// Makes a course item a course survey: it names the survey of the Surveys module the item owns. The questions, the
/// close date, anonymity and when learners see the totals are the survey's own settings.
/// </summary>
public sealed class CourseSurveyPart : ContentPart
{
    /// <summary>
    /// Gets or sets the id of the survey the item owns, created on its first save.
    /// </summary>
    public string SurveyId { get; set; }
}
