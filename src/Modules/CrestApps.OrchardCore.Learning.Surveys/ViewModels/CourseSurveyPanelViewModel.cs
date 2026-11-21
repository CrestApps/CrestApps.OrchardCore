using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Learning.Surveys.ViewModels;

/// <summary>
/// What a learner sees of a course survey.
/// </summary>
public class CourseSurveyPanelViewModel
{
    /// <summary>
    /// Gets or sets the id of the course item.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the survey, or <see langword="null"/>.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets whether the person is signed in.
    /// </summary>
    public bool SignedIn { get; set; }

    /// <summary>
    /// Gets or sets whether the person may answer now.
    /// </summary>
    public bool CanAnswer { get; set; }

    /// <summary>
    /// Gets or sets why the person cannot answer, or <see langword="null"/>.
    /// </summary>
    public string CannotAnswerReason { get; set; }

    /// <summary>
    /// Gets or sets the learner's latest response through their enrollment, or <see langword="null"/>.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets whether the survey is anonymous.
    /// </summary>
    public bool Anonymous { get; set; }
}
