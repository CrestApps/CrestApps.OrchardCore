namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// One change of a response's score.
/// </summary>
public sealed class ScoreChange
{
    /// <summary>
    /// Gets or sets when it changed, in UTC.
    /// </summary>
    public DateTime ChangedUtc { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who changed it, or a system name.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets the kind of change.
    /// </summary>
    public ScoreChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the score before.
    /// </summary>
    public decimal? From { get; set; }

    /// <summary>
    /// Gets or sets the score after.
    /// </summary>
    public decimal? To { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the id of the question graded, or null for the whole response.
    /// </summary>
    public string QuestionId { get; set; }
}
