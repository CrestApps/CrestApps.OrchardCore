using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// What a course item handler hears.
/// </summary>
public sealed class CourseItemEventContext
{
    /// <summary>
    /// Gets or sets the enrollment.
    /// </summary>
    public Enrollment Enrollment { get; set; }

    /// <summary>
    /// Gets or sets the course item.
    /// </summary>
    public ContentItem Item { get; set; }

    /// <summary>
    /// Gets or sets the kind of the item.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the progress on the item.
    /// </summary>
    public CourseItemProgress Progress { get; set; }
}
