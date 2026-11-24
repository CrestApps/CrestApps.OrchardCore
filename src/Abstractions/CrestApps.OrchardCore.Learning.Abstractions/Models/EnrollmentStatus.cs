namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// Where an enrollment stands. The moves between states are fixed by <c>EnrollmentTransitions</c>.
/// </summary>
public enum EnrollmentStatus
{
    /// <summary>
    /// The learner asked to enroll in a course that needs approval and waits for a person with the permission.
    /// </summary>
    Pending,

    /// <summary>
    /// The learner can work through the course.
    /// </summary>
    Active,

    /// <summary>
    /// Access is paused by a person with the permission.
    /// </summary>
    Suspended,

    /// <summary>
    /// The learner met the course's completion criteria. The record no longer changes.
    /// </summary>
    Completed,

    /// <summary>
    /// Access ended before the learner completed the course.
    /// </summary>
    Expired,

    /// <summary>
    /// The enrollment was rejected, withdrawn or cancelled. The record no longer changes.
    /// </summary>
    Withdrawn,
}
