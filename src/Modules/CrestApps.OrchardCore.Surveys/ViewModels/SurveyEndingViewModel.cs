using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The ending page of a response.
/// </summary>
public class SurveyEndingViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the response.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets the rendered ending.
    /// </summary>
    public SurveyEndingView Ending { get; set; }

    /// <summary>
    /// Gets or sets whether the respondent may see the review.
    /// </summary>
    public bool CanReview { get; set; }

    /// <summary>
    /// Gets or sets whether the respondent may change the response.
    /// </summary>
    public bool CanReopen { get; set; }

    /// <summary>
    /// Gets or sets whether the score shows.
    /// </summary>
    public bool ShowScore { get; set; }

    /// <summary>
    /// Gets or sets the kiosk token to return to, or <see langword="null"/>.
    /// </summary>
    public string KioskToken { get; set; }

    /// <summary>
    /// Gets or sets how long a kiosk shows the ending.
    /// </summary>
    public int KioskResetSeconds { get; set; }

    /// <summary>
    /// Gets or sets the collector token of a poll whose totals respondents may see, or <see langword="null"/>.
    /// </summary>
    public string ResultsToken { get; set; }
}
