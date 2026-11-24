namespace CrestApps.OrchardCore.Learning.Lti.Core.Services.Ags;

/// <summary>
/// A score a tool posted (Assignment and Grade Services 2.0, the score publish service), once read and checked.
/// </summary>
public sealed class LtiScoreInput
{
    /// <summary>
    /// Gets or sets the id the tool knows the learner by.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the score given, if any.
    /// </summary>
    public decimal? ScoreGiven { get; set; }

    /// <summary>
    /// Gets or sets the maximum of the score given; required with <see cref="ScoreGiven"/>.
    /// </summary>
    public decimal? ScoreMaximum { get; set; }

    /// <summary>
    /// Gets or sets the comment, if any.
    /// </summary>
    public string Comment { get; set; }

    /// <summary>
    /// Gets or sets the activity progress.
    /// </summary>
    public string ActivityProgress { get; set; }

    /// <summary>
    /// Gets or sets the grading progress.
    /// </summary>
    public string GradingProgress { get; set; }

    /// <summary>
    /// Gets or sets the tool's timestamp, in UTC.
    /// </summary>
    public DateTime TimestampUtc { get; set; }
}
