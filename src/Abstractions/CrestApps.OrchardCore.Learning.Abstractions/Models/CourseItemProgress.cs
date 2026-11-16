namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// A learner's progress on one course item.
/// </summary>
public sealed class CourseItemProgress
{
    /// <summary>
    /// Gets or sets the content item id of the course item.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public CourseItemState State { get; set; }

    /// <summary>
    /// Gets or sets when the learner first opened the item, in UTC.
    /// </summary>
    public DateTime? FirstAccessUtc { get; set; }

    /// <summary>
    /// Gets or sets when the learner last opened the item, in UTC.
    /// </summary>
    public DateTime? LastAccessUtc { get; set; }

    /// <summary>
    /// Gets or sets the time spent on the item, in seconds.
    /// </summary>
    public long SecondsSpent { get; set; }

    /// <summary>
    /// Gets or sets the share of a video the learner watched, from 0 to 100.
    /// </summary>
    public int PercentViewed { get; set; }

    /// <summary>
    /// Gets or sets the score as a percentage, when the item is graded.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Gets or sets whether the score passed, when the item is graded.
    /// </summary>
    public bool? Passed { get; set; }

    /// <summary>
    /// Gets or sets the number of attempts, when the item is graded.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets when the item was completed, in UTC.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the result was reported by the browser (a package) rather than computed on the server.
    /// Such a result never counts toward a certification unless the item allows it.
    /// </summary>
    public bool SelfReported { get; set; }

    /// <summary>
    /// Gets or sets whether the item's result (its status, score or pass) was reported by the browser, as a learning
    /// package reports its own. A share of a page or video viewed is not a result, so it never sets this.
    /// </summary>
    public bool ResultSelfReported { get; set; }

    /// <summary>
    /// Gets or sets the person who marked the item complete by hand, when it was not the learner's own work.
    /// </summary>
    public string CompletedBy { get; set; }

    /// <summary>
    /// Gets or sets why the item was marked complete by hand.
    /// </summary>
    public string CompletionReason { get; set; }

    /// <summary>
    /// Gets or sets the statement the learner confirmed, as it was shown, for an acknowledgement. It never changes once set.
    /// </summary>
    public string AttestedStatement { get; set; }

    /// <summary>
    /// Gets or sets the content item version id of the acknowledgement the learner confirmed.
    /// </summary>
    public string AttestedVersionId { get; set; }

    /// <summary>
    /// Gets or sets when the learner confirmed the acknowledgement, in UTC.
    /// </summary>
    public DateTime? AttestedUtc { get; set; }
}
