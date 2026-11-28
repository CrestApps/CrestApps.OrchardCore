using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The landing page of a survey link.
/// </summary>
public class SurveyLandingViewModel
{
    /// <summary>
    /// Gets or sets the collector token.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the respondent title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the sanitized introduction.
    /// </summary>
    public string Introduction { get; set; }

    /// <summary>
    /// Gets or sets whether a password is needed.
    /// </summary>
    public bool NeedsPassword { get; set; }

    /// <summary>
    /// Gets or sets whether a CAPTCHA is needed.
    /// </summary>
    public bool NeedsCaptcha { get; set; }

    /// <summary>
    /// Gets or sets the CSS class of the published version, or <see langword="null"/>.
    /// </summary>
    public string CssClass { get; set; }

    /// <summary>
    /// Gets or sets the error message, or <see langword="null"/>.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets whether the respondent can start.
    /// </summary>
    public bool CanStart { get; set; } = true;

    /// <summary>
    /// Gets or sets the query string to keep on the start post (hidden variables).
    /// </summary>
    public string QueryString { get; set; }

    /// <summary>
    /// Gets or sets whether the survey shows totals to respondents.
    /// </summary>
    public bool ShowResultsLink { get; set; }

    /// <summary>
    /// Gets or sets whether the link goes straight to the first page.
    /// </summary>
    public bool SkipLanding { get; set; }

    /// <summary>
    /// Gets or sets whether the page posts its start form at once (the survey skips its landing page). The form keeps
    /// a visible Start button for browsers without script.
    /// </summary>
    public bool AutoStart { get; set; }
}
