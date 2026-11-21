using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// The ordered sections of a course. Each course item is its own content item contained in the course through
/// Orchard's list part; the outline only orders their ids into sections and says which are required. It is versioned
/// with the course.
/// </summary>
public sealed class CourseOutlinePart : ContentPart
{
    /// <summary>
    /// Gets or sets the sections, in order.
    /// </summary>
    public IList<CourseSection> Sections { get; set; } = [];
}
