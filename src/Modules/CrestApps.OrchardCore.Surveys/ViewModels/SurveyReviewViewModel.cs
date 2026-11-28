using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The review page of a completed response.
/// </summary>
public class SurveyReviewViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the respondent title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the response.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets what the review shows.
    /// </summary>
    public ReviewShows Shows { get; set; }

    /// <summary>
    /// Gets or sets the question shapes, in the order the respondent saw them.
    /// </summary>
    public IList<IShape> Questions { get; set; } = [];
}
