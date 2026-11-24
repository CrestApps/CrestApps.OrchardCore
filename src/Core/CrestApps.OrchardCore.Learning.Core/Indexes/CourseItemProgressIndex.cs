using CrestApps.OrchardCore.Learning.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Core.Indexes;

/// <summary>
/// One row per course item a learner opened, for reports.
/// </summary>
public sealed class CourseItemProgressIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the enrollment.
    /// </summary>
    public string EnrollmentId { get; set; }

    /// <summary>
    /// Gets or sets the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the course item.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public CourseItemState State { get; set; }

    /// <summary>
    /// Gets or sets when the learner last opened the item, in UTC; the first access when never reopened.
    /// </summary>
    public DateTime LastAccessUtc { get; set; }

    /// <summary>
    /// Gets or sets when the item was completed, in UTC.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the time spent, in seconds.
    /// </summary>
    public long SecondsSpent { get; set; }

    /// <summary>
    /// Gets or sets the score as a percentage.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Gets or sets whether the result was self-reported.
    /// </summary>
    public bool SelfReported { get; set; }
}
