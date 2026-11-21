using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// The content of a survey: its pages, questions, logic, endings and versioned settings. A draft and every published version hold one.
/// </summary>
public sealed class SurveyDefinition
{
    /// <summary>
    /// Gets or sets the version of this shape, for future changes.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the title respondents see.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the sanitized HTML respondents see on the landing page.
    /// </summary>
    public string Introduction { get; set; }

    /// <summary>
    /// Gets or sets the pages in their designed order.
    /// </summary>
    public List<SurveyPage> Pages { get; set; } = [];

    /// <summary>
    /// Gets or sets the hidden and calculated variables.
    /// </summary>
    public List<SurveyVariable> Variables { get; set; } = [];

    /// <summary>
    /// Gets or sets the endings; one is the default.
    /// </summary>
    public List<SurveyEnding> Endings { get; set; } = [];

    /// <summary>
    /// Gets or sets the score categories questions may add to.
    /// </summary>
    public List<ScoreCategory> ScoreCategories { get; set; } = [];

    /// <summary>
    /// Gets or sets the score bands that give a result message by percent.
    /// </summary>
    public List<ScoreBand> ScoreBands { get; set; } = [];

    /// <summary>
    /// Gets or sets the quotas that stop or screen out responses once a count is reached.
    /// </summary>
    public List<SurveyQuota> Quotas { get; set; } = [];

    /// <summary>
    /// Gets or sets how the survey is presented.
    /// </summary>
    public SurveyPresentationSettings Presentation { get; set; } = new();

    /// <summary>
    /// Gets or sets how respondents move through the survey.
    /// </summary>
    public SurveyNavigationSettings Navigation { get; set; } = new();

    /// <summary>
    /// Gets or sets how responses are scored.
    /// </summary>
    public SurveyScoringSettings Scoring { get; set; } = new();

    /// <summary>
    /// Gets or sets what respondents may see of their answers and the results.
    /// </summary>
    public SurveyReviewSettings Review { get; set; } = new();

    /// <summary>
    /// Gets or sets the time limit.
    /// </summary>
    public SurveyTimingSettings Timing { get; set; } = new();

    /// <summary>
    /// Gets or sets the translated texts: culture, then text key, then text.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Translations { get; set; } = [];

    /// <summary>
    /// Gets or sets settings of consumers that must be versioned with the definition.
    /// </summary>
    public JsonObject Properties { get; set; } = [];
}
