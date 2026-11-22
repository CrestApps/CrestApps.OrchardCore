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
    /// Gets or sets the URL of the page with the survey's totals, or <see langword="null"/> when the respondent may not see them.
    /// </summary>
    public string ResultsUrl { get; set; }

    /// <summary>
    /// Gets or sets the local URL a consumer asked the respondent to return to, or <see langword="null"/>.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the text of the link back, or <see langword="null"/> for the default text.
    /// </summary>
    public string ReturnText { get; set; }
}
