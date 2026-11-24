namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// A person on the staff of a course, by user id only. With Staff on, the editor picks staff members with the Instructor
/// capability and stores their linked user.
/// </summary>
public sealed class CourseStaffMember
{
    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the role, an open string such as <c>Instructor</c>, <c>Assistant</c>, <c>Grader</c> or <c>Observer</c>.
    /// </summary>
    public string Role { get; set; }
}
