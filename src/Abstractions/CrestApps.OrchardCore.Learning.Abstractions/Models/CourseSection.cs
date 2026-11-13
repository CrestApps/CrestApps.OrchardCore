namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// A section of a course outline.
/// </summary>
public sealed class CourseSection
{
    /// <summary>
    /// Gets or sets the id of the section, unique in the course.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets a short summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the rules that must all be met before the section's items open.
    /// </summary>
    public IList<LearningRuleSettings> ReleaseRules { get; set; } = [];

    /// <summary>
    /// Gets or sets the course items of the section, in order.
    /// </summary>
    public IList<CourseOutlineItem> Items { get; set; } = [];
}
