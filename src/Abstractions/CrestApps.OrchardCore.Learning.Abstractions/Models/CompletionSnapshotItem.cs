namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// One course item in a <see cref="CompletionSnapshot"/>.
/// </summary>
public sealed class CompletionSnapshotItem
{
    /// <summary>
    /// Gets or sets the content item id of the course item.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the title of the course item at completion.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the kind of the course item.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the title of the section the item was in.
    /// </summary>
    public string Section { get; set; }

    /// <summary>
    /// Gets or sets whether the item was required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the state the learner reached.
    /// </summary>
    public CourseItemState State { get; set; }

    /// <summary>
    /// Gets or sets the score as a percentage, when the item is graded.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Gets or sets whether the result was reported by the browser rather than computed on the server.
    /// </summary>
    public bool SelfReported { get; set; }

    /// <summary>
    /// Gets or sets whether the item's result (its status, score or pass) was reported by the browser.
    /// </summary>
    public bool ResultSelfReported { get; set; }

    /// <summary>
    /// Gets or sets whether the item let a result reported by the browser count toward a certification, as it was set
    /// at completion.
    /// </summary>
    public bool AllowSelfReported { get; set; }

    /// <summary>
    /// Gets or sets when the item was completed, in UTC.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
}
