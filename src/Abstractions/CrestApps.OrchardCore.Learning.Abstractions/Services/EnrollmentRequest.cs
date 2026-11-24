namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Asks to enroll a learner in a course, or to add a grant to the open enrollment the learner already has.
/// </summary>
public sealed class EnrollmentRequest
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
    /// Gets or sets the course offering, when the course has offerings.
    /// </summary>
    public string OfferingId { get; set; }

    /// <summary>
    /// Gets or sets the enrollment source, such as <c>Manual</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the id of the record that grants access in its source. The same source and id never add a second grant.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets when the grant's access ends, in UTC, or <see langword="null"/> for no end.
    /// </summary>
    public DateTime? AccessEndsUtc { get; set; }

    /// <summary>
    /// Gets or sets when the course is due, in UTC.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the enrollment waits for approval. A self enrollment in a course that needs approval does.
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// Gets or sets whether to skip the course's prerequisites, for a person with the permission.
    /// </summary>
    public bool IgnorePrerequisites { get; set; }

    /// <summary>
    /// Gets or sets the enrollment a renewal or retake follows. Empty means the learner's latest closed enrollment in the course.
    /// </summary>
    public string PreviousEnrollmentId { get; set; }

    /// <summary>
    /// Gets or sets the user id of the person enrolling, or the name of the process.
    /// </summary>
    public string Actor { get; set; }
}
