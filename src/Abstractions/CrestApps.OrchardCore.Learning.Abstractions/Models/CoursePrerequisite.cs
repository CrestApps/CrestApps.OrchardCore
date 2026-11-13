namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// Something a learner must hold before enrolling in a course: another course completed, a certification or a skill.
/// </summary>
public sealed class CoursePrerequisite
{
    /// <summary>
    /// Gets or sets the kind, an open string such as <c>Course</c>, <c>Certification</c> or <c>Skill</c>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the id of the course, certification or skill.
    /// </summary>
    public string Id { get; set; }
}
