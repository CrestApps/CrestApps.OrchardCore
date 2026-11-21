using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Core.Indexes;

/// <summary>
/// The YesSql index of enrollments.
/// </summary>
public sealed class EnrollmentIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the offering, or <see langword="null"/>.
    /// </summary>
    public string OfferingId { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public EnrollmentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets whether the enrollment is open: pending, active or suspended.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Gets or sets whether the enrollment has an open review.
    /// </summary>
    public bool NeedsReview { get; set; }

    /// <summary>
    /// Gets or sets the enrollment number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the pass number.
    /// </summary>
    public int Cycle { get; set; }

    /// <summary>
    /// Gets or sets when the enrollment was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when access ends, in UTC.
    /// </summary>
    public DateTime? AccessEndsUtc { get; set; }

    /// <summary>
    /// Gets or sets when the course is due, in UTC.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets when the course was completed, in UTC.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the learner last worked on the course, in UTC.
    /// </summary>
    public DateTime? LastActivityUtc { get; set; }

    /// <summary>
    /// Gets or sets the share of required items completed.
    /// </summary>
    public int ProgressPercent { get; set; }
}
