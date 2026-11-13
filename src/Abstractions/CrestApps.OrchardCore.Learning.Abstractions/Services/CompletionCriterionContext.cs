using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// What a completion criterion decides on.
/// </summary>
public sealed class CompletionCriterionContext
{
    /// <summary>
    /// Gets or sets the criterion's settings.
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
    /// Gets or sets the course.
    /// </summary>
    public ContentItem Course { get; set; }

    /// <summary>
    /// Gets or sets the outline of the course.
    /// </summary>
    public CourseOutline Outline { get; set; }

    /// <summary>
    /// Gets or sets the course grade, when it has one.
    /// </summary>
    public decimal? Grade { get; set; }
}
