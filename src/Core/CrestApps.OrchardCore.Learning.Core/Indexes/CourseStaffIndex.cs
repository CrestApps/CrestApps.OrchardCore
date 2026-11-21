using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Core.Indexes;

/// <summary>
/// One row per person on the staff of a course version, so authorization and lists find a user's courses by id.
/// </summary>
public sealed class CourseStaffIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the content item id of the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the role.
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    /// Gets or sets whether the course version is published.
    /// </summary>
    public bool Published { get; set; }

    /// <summary>
    /// Gets or sets whether the course version is the latest.
    /// </summary>
    public bool Latest { get; set; }
}
