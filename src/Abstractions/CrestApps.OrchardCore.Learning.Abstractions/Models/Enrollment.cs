using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// One pass of a learner through a course. Taking the course again creates a new enrollment that links to this one;
/// a completed or withdrawn enrollment never changes. The learner is a user id; names are resolved when shown.
/// Progress is kept in its own <see cref="EnrollmentProgress"/> document, so progress writes never touch this one.
/// </summary>
public sealed class Enrollment : CatalogItem
{
    /// <summary>
    /// Gets or sets the user id of the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the course offering (run), or <see langword="null"/> for a course that has none.
    /// </summary>
    public string OfferingId { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public EnrollmentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the enrollment number, from a locked number sequence.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets when the enrollment was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the enrollment last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the learner first opened a course item, in UTC.
    /// </summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when access ends, in UTC: the latest end of its grants, or <see langword="null"/> when a grant has
    /// no end.
    /// </summary>
    public DateTime? AccessEndsUtc { get; set; }

    /// <summary>
    /// Gets or sets when the course is due, in UTC.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets when the learner completed the course, in UTC.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the learner last worked on the course, in UTC.
    /// </summary>
    public DateTime? LastActivityUtc { get; set; }

    /// <summary>
    /// Gets or sets the share of required items completed, from 0 to 100.
    /// </summary>
    public int ProgressPercent { get; set; }

    /// <summary>
    /// Gets or sets the course grade as a percentage, when the course has gradable items.
    /// </summary>
    public decimal? Grade { get; set; }

    /// <summary>
    /// Gets or sets whether the grade met the course pass mark, when the course has one.
    /// </summary>
    public bool? Passed { get; set; }

    /// <summary>
    /// Gets or sets the pass number: 1 for the first enrollment of the learner in the course, 2 for a retake or renewal.
    /// </summary>
    public int Cycle { get; set; } = 1;

    /// <summary>
    /// Gets or sets the enrollment this one follows, for a retake or a renewal.
    /// </summary>
    public string PreviousEnrollmentId { get; set; }

    /// <summary>
    /// Gets or sets the reasons the learner has access. Access stays while any grant remains.
    /// </summary>
    public IList<EnrollmentGrant> Grants { get; set; } = [];

    /// <summary>
    /// Gets or sets the changes of state, oldest first.
    /// </summary>
    public IList<EnrollmentStatusChange> History { get; set; } = [];

    /// <summary>
    /// Gets or sets what the enrollment proves, once it is completed.
    /// </summary>
    public CompletionSnapshot Completion { get; set; }

    /// <summary>
    /// Gets a value indicating whether the enrollment is open: not completed, expired or withdrawn.
    /// </summary>
    public bool IsOpen => Status is EnrollmentStatus.Pending or EnrollmentStatus.Active or EnrollmentStatus.Suspended;

    /// <summary>
    /// Gets or sets the review of a completed enrollment whose result changed after completion, or
    /// <see langword="null"/>.
    /// </summary>
    public EnrollmentReview Review { get; set; }

    /// <summary>
    /// Gets whether the enrollment has a review nobody has cleared yet.
    /// </summary>
    public bool NeedsReview => Review is { ClearedUtc: null };
}
