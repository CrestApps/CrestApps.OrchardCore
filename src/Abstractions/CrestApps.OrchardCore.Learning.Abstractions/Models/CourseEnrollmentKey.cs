using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// The hash of the enrollment key of a course. It is kept out of the course content item, so the content API, content
/// exports and templates that print the course never carry it.
/// </summary>
public sealed class CourseEnrollmentKey : CatalogItem
{
    /// <summary>
    /// Gets or sets the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the salted hash of the key.
    /// </summary>
    public string Hash { get; set; }

    /// <summary>
    /// Gets or sets when the key was last set, in UTC.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }
}
