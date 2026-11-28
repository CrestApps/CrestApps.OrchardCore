namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// How responses are scored.
/// </summary>
public sealed class SurveyScoringSettings
{
    /// <summary>
    /// Gets or sets whether responses are scored.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the percent a response needs to pass, or null for no pass mark.
    /// </summary>
    public decimal? PassMarkPercent { get; set; }

    /// <summary>
    /// Gets or sets the points of a scorable question without points of its own.
    /// </summary>
    public decimal DefaultPoints { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether wrong choices take points away.
    /// </summary>
    public bool NegativeMarking { get; set; }

    /// <summary>
    /// Gets or sets whether respondents see their score when the review settings allow it.
    /// </summary>
    public bool ShowScoreToRespondent { get; set; } = true;

    /// <summary>
    /// Gets or sets whether answers are checked at the end or one at a time.
    /// </summary>
    public FeedbackMode FeedbackMode { get; set; }

    /// <summary>
    /// Gets or sets the decimals scores are rounded to.
    /// </summary>
    public int RoundingDecimals { get; set; } = 2;

    /// <summary>
    /// Gets or sets a formula over category scores that gives the total score; the sum when empty.
    /// </summary>
    public string ScoreFormula { get; set; }
}
