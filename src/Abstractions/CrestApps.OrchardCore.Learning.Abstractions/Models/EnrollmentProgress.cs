using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// A learner's progress on each course item of one enrollment, in a document of its own so that progress writes never
/// touch the enrollment, the course or any shared document.
/// </summary>
public sealed class EnrollmentProgress : CatalogItem
{
    /// <summary>
    /// Gets or sets the enrollment.
    /// </summary>
    public string EnrollmentId { get; set; }

    /// <summary>
    /// Gets or sets the user id of the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the progress on each course item the learner opened.
    /// </summary>
    public IList<CourseItemProgress> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets when the progress last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Finds the progress on a course item.
    /// </summary>
    /// <param name="contentItemId">The content item id of the course item.</param>
    /// <returns>The progress, or <see langword="null"/> when the learner never opened the item.</returns>
    public CourseItemProgress Find(string contentItemId)
        => Items.FirstOrDefault(item => string.Equals(item.ContentItemId, contentItemId, StringComparison.Ordinal));
}
