using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// One course item of a resolved outline.
/// </summary>
public sealed class CourseOutlineEntry
{
    /// <summary>
    /// Gets or sets the section the item is in.
    /// </summary>
    public CourseSection Section { get; set; }

    /// <summary>
    /// Gets or sets the course item.
    /// </summary>
    public ContentItem Item { get; set; }

    /// <summary>
    /// Gets or sets the course item part of the item.
    /// </summary>
    public CourseItemPart Part { get; set; }

    /// <summary>
    /// Gets or sets whether the item counts toward completion.
    /// </summary>
    public bool Required { get; set; }
}
