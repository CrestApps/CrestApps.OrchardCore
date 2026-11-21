namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// One page of a survey.
/// </summary>
public sealed class SurveyPage
{
    /// <summary>
    /// Gets or sets the stable id; it never changes.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the variable code, unique within the survey.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the sanitized HTML shown under the title; supports piping.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets when the page shows.
    /// </summary>
    public SurveyVisibility Visibility { get; set; } = new();

    /// <summary>
    /// Gets or sets the questions in display order; pool members carry their pool id.
    /// </summary>
    public List<SurveyQuestion> Questions { get; set; } = [];

    /// <summary>
    /// Gets or sets the question pools of the page.
    /// </summary>
    public List<QuestionPool> Pools { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the page shows its questions in order or shuffled.
    /// </summary>
    public QuestionOrder QuestionOrder { get; set; }

    /// <summary>
    /// Gets or sets the branches evaluated in order when the page is left.
    /// </summary>
    public List<SurveyBranch> Branches { get; set; } = [];

    /// <summary>
    /// Gets or sets whether respondents may go back from this page (also needs the survey to allow it).
    /// </summary>
    public bool AllowBack { get; set; } = true;

    /// <summary>
    /// Gets or sets the time a respondent may spend on the page, or null for no limit.
    /// </summary>
    public int? TimeLimitSeconds { get; set; }
}
