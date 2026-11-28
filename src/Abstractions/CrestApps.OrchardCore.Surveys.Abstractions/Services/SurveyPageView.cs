using CrestApps.OrchardCore.Conditions;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// What a respondent sees of the current page.
/// </summary>
public sealed class SurveyPageView
{
    /// <summary>
    /// Gets or sets the id of the page (or screen).
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the piped title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the piped description (sanitized HTML).
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets every question of the page in plan order, including those hidden now (the runner hides them and
    /// shows them as answers change).
    /// </summary>
    public List<SurveyQuestionView> Questions { get; set; } = [];

    /// <summary>
    /// Gets or sets the ids of the questions hidden now.
    /// </summary>
    public List<string> HiddenQuestionIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the ids of the choices hidden now, by question id.
    /// </summary>
    public Dictionary<string, List<string>> HiddenChoiceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the plain rules the browser may evaluate without a round trip, by key
    /// (<c>q:{questionId}</c>, <c>c:{questionId}:{choiceId}</c>).
    /// </summary>
    public Dictionary<string, ConditionRule> ClientRules { get; set; } = [];

    /// <summary>
    /// Gets or sets the keys whose rules need the server (formulas, calculated variables, piping).
    /// </summary>
    public List<string> ServerRuleKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets the position of the page, from 1.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets how many pages the response has.
    /// </summary>
    public int PageCount { get; set; }

    /// <summary>
    /// Gets or sets the progress, from 0 to 100.
    /// </summary>
    public int ProgressPercent { get; set; }

    /// <summary>
    /// Gets or sets whether the respondent may go back.
    /// </summary>
    public bool CanGoBack { get; set; }

    /// <summary>
    /// Gets or sets whether this is the last page.
    /// </summary>
    public bool IsLast { get; set; }

    /// <summary>
    /// Gets or sets when time runs out, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? DeadlineUtc { get; set; }
}
