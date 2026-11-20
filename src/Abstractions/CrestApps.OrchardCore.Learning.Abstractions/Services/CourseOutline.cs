using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// A course with its outline resolved: each section with its published course items, in order.
/// </summary>
public sealed class CourseOutline
{
    /// <summary>
    /// Gets or sets the course.
    /// </summary>
    public ContentItem Course { get; set; }

    /// <summary>
    /// Gets or sets the items of the outline, in order, with the section each is in.
    /// </summary>
    public IList<CourseOutlineEntry> Entries { get; set; } = [];

    /// <summary>
    /// Gets the entries that count toward completion.
    /// </summary>
    public IEnumerable<CourseOutlineEntry> RequiredEntries => Entries.Where(entry => entry.Required && !entry.Inline);

    /// <summary>
    /// Finds the entry of a course item.
    /// </summary>
    /// <param name="contentItemId">The content item id of the course item.</param>
    /// <returns>The entry, or <see langword="null"/> when the item is not in the outline.</returns>
    public CourseOutlineEntry Find(string contentItemId)
        => Entries.FirstOrDefault(entry => string.Equals(entry.Item.ContentItemId, contentItemId, StringComparison.Ordinal));
}
