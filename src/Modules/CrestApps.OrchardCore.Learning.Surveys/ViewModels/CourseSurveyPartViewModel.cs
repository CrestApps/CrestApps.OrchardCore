using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Learning.Surveys.ViewModels;

/// <summary>
/// The editor of a course survey: a summary of the survey the item owns and links to its pages.
/// </summary>
public class CourseSurveyPartViewModel
{
    /// <summary>
    /// Gets or sets the survey, or <see langword="null"/> before the item is first saved.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets whether the item is a one-question choice.
    /// </summary>
    public bool SingleQuestion { get; set; }

    /// <summary>
    /// Gets or sets how many questions the draft has.
    /// </summary>
    public int QuestionCount { get; set; }
}
