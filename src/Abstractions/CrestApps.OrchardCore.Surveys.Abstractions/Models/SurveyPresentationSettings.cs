namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// How a survey is presented to respondents.
/// </summary>
public sealed class SurveyPresentationSettings
{
    /// <summary>
    /// Gets or sets whether the survey title shows.
    /// </summary>
    public bool ShowTitle { get; set; } = true;

    /// <summary>
    /// Gets or sets whether questions are numbered.
    /// </summary>
    public bool ShowQuestionNumbers { get; set; } = true;

    /// <summary>
    /// Gets or sets whether page titles show.
    /// </summary>
    public bool ShowPageTitles { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a progress bar shows.
    /// </summary>
    public bool ShowProgressBar { get; set; } = true;

    /// <summary>
    /// Gets or sets what the progress bar counts.
    /// </summary>
    public ProgressBasis ProgressBasis { get; set; }

    /// <summary>
    /// Gets or sets how many questions show on each screen; 0 keeps the designed pages.
    /// </summary>
    public int QuestionsPerPage { get; set; }

    /// <summary>
    /// Gets or sets whether pages show in order or shuffled (first and last pages keep their place).
    /// </summary>
    public QuestionOrder PageOrder { get; set; }

    /// <summary>
    /// Gets or sets whether a link goes straight to the first page instead of the landing page.
    /// </summary>
    public bool SkipLanding { get; set; }

    /// <summary>
    /// Gets or sets a CSS class added to the runner.
    /// </summary>
    public string CssClass { get; set; }

    /// <summary>
    /// Gets or sets a URL respondents are sent to after any ending without a redirect of its own.
    /// </summary>
    public string CompletedRedirectUrl { get; set; }
}
