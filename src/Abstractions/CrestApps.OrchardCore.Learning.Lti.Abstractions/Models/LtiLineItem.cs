using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Models;

/// <summary>
/// A line item of Assignment and Grade Services: a column of a course's grades that a tool reads and posts scores to.
/// A line item bound to an External tool course item (<see cref="ResourceLinkId"/>) passes its fully graded scores to
/// the learner's progress.
/// </summary>
public sealed class LtiLineItem : CatalogItem
{
    /// <summary>
    /// Gets or sets the id of the <see cref="ExternalTool"/> the line item belongs to.
    /// </summary>
    public string ToolId { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the course (the LTI context).
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the External tool course item (the resource link), or <see langword="null"/>.
    /// </summary>
    public string ResourceLinkId { get; set; }

    /// <summary>
    /// Gets or sets the tool's own id of the resource, if any.
    /// </summary>
    public string ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the tool's tag, if any.
    /// </summary>
    public string Tag { get; set; }

    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the maximum score, more than 0.
    /// </summary>
    public decimal ScoreMaximum { get; set; }

    /// <summary>
    /// Gets or sets when submissions start, in UTC, if the tool gave it.
    /// </summary>
    public DateTime? StartDateTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets when submissions end, in UTC, if the tool gave it.
    /// </summary>
    public DateTime? EndDateTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets when the line item was made, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
