using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Core.Indexes;

/// <summary>
/// The YesSql index of enrollment progress documents.
/// </summary>
public sealed class EnrollmentProgressIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the enrollment.
    /// </summary>
    public string EnrollmentId { get; set; }

    /// <summary>
    /// Gets or sets the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the course.
    /// </summary>
    public string CourseId { get; set; }
}
