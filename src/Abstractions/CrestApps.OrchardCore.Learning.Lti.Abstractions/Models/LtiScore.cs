using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Models;

/// <summary>
/// The latest score a tool posted for a learner on a line item. Each accepted score replaces the one before, because its
/// timestamp is later; the result a tool reads is made from it.
/// </summary>
public sealed class LtiScore : CatalogItem
{
    /// <summary>
    /// Gets or sets the id of the <see cref="LtiLineItem"/>.
    /// </summary>
    public string LineItemId { get; set; }

    /// <summary>
    /// Gets or sets the id of the <see cref="ExternalTool"/> that posted it.
    /// </summary>
    public string ToolId { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the user id of the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the id the tool knows the learner by (its <c>userId</c>).
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the score given, if any.
    /// </summary>
    public decimal? ScoreGiven { get; set; }

    /// <summary>
    /// Gets or sets the maximum of the score given.
    /// </summary>
    public decimal? ScoreMaximum { get; set; }

    /// <summary>
    /// Gets or sets the tool's comment, if any.
    /// </summary>
    public string Comment { get; set; }

    /// <summary>
    /// Gets or sets the activity progress: <c>Initialized</c>, <c>Started</c>, <c>InProgress</c>, <c>Submitted</c> or
    /// <c>Completed</c>.
    /// </summary>
    public string ActivityProgress { get; set; }

    /// <summary>
    /// Gets or sets the grading progress: <c>FullyGraded</c>, <c>Pending</c>, <c>PendingManual</c>, <c>Failed</c> or
    /// <c>NotReady</c>.
    /// </summary>
    public string GradingProgress { get; set; }

    /// <summary>
    /// Gets or sets the tool's timestamp of the score, in UTC.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Gets or sets when the platform received it, in UTC.
    /// </summary>
    public DateTime ReceivedUtc { get; set; }
}
