using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The totals respondents may see of a poll or survey.
/// </summary>
public class SurveyResultsViewModel
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
    /// Gets or sets how many responses were counted.
    /// </summary>
    public long Responses { get; set; }

    /// <summary>
    /// Gets or sets the totals of each choice question.
    /// </summary>
    public IList<SurveyResultsQuestion> Questions { get; set; } = [];
}
