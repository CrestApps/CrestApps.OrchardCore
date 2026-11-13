using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Whether a person may open a course item, and why.
/// </summary>
public sealed class CourseItemAccess
{
    /// <summary>
    /// Gets a value indicating whether the person may open the item.
    /// </summary>
    public bool Allowed => Reason is CourseItemAccessReason.Staff or CourseItemAccessReason.FreePreview or CourseItemAccessReason.Enrolled;

    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    public CourseItemAccessReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the learner's open enrollment in the course, when there is one.
    /// </summary>
    public Enrollment Enrollment { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets what the learner must do first, when the item is not open yet.
    /// </summary>
    public string LockedReason { get; set; }

    /// <summary>
    /// Gets or sets when the item opens, in UTC, when that is known.
    /// </summary>
    public DateTime? AvailableFromUtc { get; set; }
}
