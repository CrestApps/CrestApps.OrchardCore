using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// What a progress call changed.
/// </summary>
public sealed class ProgressUpdate
{
    /// <summary>
    /// Gets or sets the enrollment, as it is after the call.
    /// </summary>
    public Enrollment Enrollment { get; set; }

    /// <summary>
    /// Gets or sets the progress of the enrollment, as it is after the call.
    /// </summary>
    public EnrollmentProgress Progress { get; set; }

    /// <summary>
    /// Gets or sets the progress on the course item, as it is after the call.
    /// </summary>
    public CourseItemProgress Item { get; set; }

    /// <summary>
    /// Gets or sets whether the call completed the course item.
    /// </summary>
    public bool ItemCompleted { get; set; }

    /// <summary>
    /// Gets or sets whether the call completed the course.
    /// </summary>
    public bool CourseCompleted { get; set; }

    /// <summary>
    /// Gets or sets the reason the call was refused, such as an item that is not open yet; <see langword="null"/> when it was accepted.
    /// </summary>
    public string RefusedReason { get; set; }
}
