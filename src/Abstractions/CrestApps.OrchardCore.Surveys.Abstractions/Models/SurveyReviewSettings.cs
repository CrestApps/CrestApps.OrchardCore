namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// What respondents may see of their answers, the key and the results, and when.
/// </summary>
public sealed class SurveyReviewSettings
{
    /// <summary>
    /// Gets or sets what shows while the response is in progress (immediate feedback).
    /// </summary>
    public ReviewShows During { get; set; }

    /// <summary>
    /// Gets or sets what shows right after the response is completed.
    /// </summary>
    public ReviewShows ImmediatelyAfter { get; set; } = ReviewShows.Score | ReviewShows.Responses;

    /// <summary>
    /// Gets or sets what shows later while the survey is open.
    /// </summary>
    public ReviewShows LaterWhileOpen { get; set; } = ReviewShows.Score | ReviewShows.Responses;

    /// <summary>
    /// Gets or sets what shows after the survey closes.
    /// </summary>
    public ReviewShows AfterClose { get; set; } = ReviewShows.All;

    /// <summary>
    /// Gets or sets when respondents may see the totals of all responses.
    /// </summary>
    public ResultsVisibility ResultsVisibility { get; set; }
}
