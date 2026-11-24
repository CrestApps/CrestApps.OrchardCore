namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Why a person may or may not open a course item.
/// </summary>
public enum CourseItemAccessReason
{
    /// <summary>
    /// The item is not in a course, so Learning does not decide.
    /// </summary>
    NotACourseItem,

    /// <summary>
    /// The person authors courses or is on the course staff.
    /// </summary>
    Staff,

    /// <summary>
    /// The item is a free preview.
    /// </summary>
    FreePreview,

    /// <summary>
    /// The person has an active enrollment and the item is open.
    /// </summary>
    Enrolled,

    /// <summary>
    /// The person is not signed in.
    /// </summary>
    Anonymous,

    /// <summary>
    /// The person has no open enrollment in the course.
    /// </summary>
    NotEnrolled,

    /// <summary>
    /// The enrollment waits for approval.
    /// </summary>
    Pending,

    /// <summary>
    /// The enrollment is suspended.
    /// </summary>
    Suspended,

    /// <summary>
    /// The enrollment's access has ended.
    /// </summary>
    AccessEnded,

    /// <summary>
    /// The item's release rules are not met yet.
    /// </summary>
    NotReleased,

    /// <summary>
    /// A course item that is not in a course; only course managers and people who may edit it open it.
    /// </summary>
    NotInCourse,
}
