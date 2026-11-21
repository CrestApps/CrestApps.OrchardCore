using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// What a release rule decides on.
/// </summary>
public sealed class ReleaseRuleContext
{
    /// <summary>
    /// Gets or sets the rule's settings.
    /// </summary>
    public LearningRuleSettings Settings { get; set; }

    /// <summary>
    /// Gets or sets the enrollment.
    /// </summary>
    public Enrollment Enrollment { get; set; }

    /// <summary>
    /// Gets or sets the progress of the enrollment.
    /// </summary>
    public EnrollmentProgress Progress { get; set; }

    /// <summary>
    /// Gets or sets the course item, or <see langword="null"/> when the rule is on a section.
    /// </summary>
    public ContentItem Item { get; set; }

    /// <summary>
    /// Gets or sets the current time, in UTC.
    /// </summary>
    public DateTime UtcNow { get; set; }
}
