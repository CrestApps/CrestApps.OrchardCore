using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The scoring, review and timing settings of a survey definition.
/// </summary>
public class SurveyDefinitionScoringEditViewModel
{
    /// <summary>
    /// Gets or sets the scoring settings.
    /// </summary>
    public SurveyScoringSettings Scoring { get; set; } = new();

    /// <summary>
    /// Gets or sets the timing settings.
    /// </summary>
    public SurveyTimingSettings Timing { get; set; } = new();

    /// <summary>
    /// Gets or sets when respondents may see the totals.
    /// </summary>
    public ResultsVisibility ResultsVisibility { get; set; }

    /// <summary>
    /// Gets or sets what shows during the response.
    /// </summary>
    public List<ReviewShows> ReviewDuring { get; set; } = [];

    /// <summary>
    /// Gets or sets what shows right after the response.
    /// </summary>
    public List<ReviewShows> ReviewImmediatelyAfter { get; set; } = [];

    /// <summary>
    /// Gets or sets what shows later while the survey is open.
    /// </summary>
    public List<ReviewShows> ReviewLaterWhileOpen { get; set; } = [];

    /// <summary>
    /// Gets or sets what shows after the survey closes.
    /// </summary>
    public List<ReviewShows> ReviewAfterClose { get; set; } = [];
}
