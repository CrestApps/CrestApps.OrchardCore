using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// The filters of an enrollment list.
/// </summary>
public sealed class EnrollmentQuery
{
    /// <summary>
    /// Gets or sets the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the courses the caller may see, when the caller's scope is limited. <see langword="null"/> means every course.
    /// </summary>
    public IReadOnlyCollection<string> CourseIds { get; set; }

    /// <summary>
    /// Gets or sets the learners the caller may see, when the caller's scope is limited. <see langword="null"/> means everyone.
    /// </summary>
    public IReadOnlyCollection<string> UserIds { get; set; }

    /// <summary>
    /// Gets or sets the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public EnrollmentStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets an enrollment number to find.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets whether to list only enrollments with an open review (<see langword="true"/>) or only those without
    /// one (<see langword="false"/>), or <see langword="null"/> for both.
    /// </summary>
    public bool? NeedsReview { get; set; }

    /// <summary>
    /// Gets or sets a time the due date must be before, in UTC, to list overdue enrollments; enrollments without a due
    /// date are left out.
    /// </summary>
    public DateTime? DueBeforeUtc { get; set; }

    /// <summary>
    /// Gets or sets a time access must end after, in UTC; enrollments without an end are left out.
    /// </summary>
    public DateTime? AccessEndsAfterUtc { get; set; }

    /// <summary>
    /// Gets or sets a time access must end by, in UTC; enrollments without an end are left out.
    /// </summary>
    public DateTime? AccessEndsBeforeUtc { get; set; }

    /// <summary>
    /// Gets or sets the order of the page. The default is the newest first.
    /// </summary>
    public EnrollmentOrder Order { get; set; }
}
