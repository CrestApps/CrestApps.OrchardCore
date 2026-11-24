namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// A course item in a section of the outline.
/// </summary>
public sealed class CourseOutlineItem
{
    /// <summary>
    /// Gets or sets the content item id of the course item.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets whether the item counts toward completion.
    /// </summary>
    public bool Required { get; set; } = true;
}
