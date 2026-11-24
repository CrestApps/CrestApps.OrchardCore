using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Deployments;

/// <summary>
/// Exports enrollments with their progress. Learner records hold personal data, so the step is chosen on purpose, can be
/// narrowed to a course and a date range, needs the Export learner records permission and is recorded when it runs.
/// </summary>
public sealed class LearningEnrollmentsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LearningEnrollmentsDeploymentStep>("Learning");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LearningEnrollmentsDeploymentStep>("Learning enrollments (learner records)");

    /// <inheritdoc />
    public override string Name => LearningConstants.EnrollmentsRecipeStep;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets the course to export, or <see langword="null"/> for every course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the earliest enrollment date to export, in UTC.
    /// </summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the latest enrollment date to export, in UTC.
    /// </summary>
    public DateTime? ToUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the progress on each course item is exported too.
    /// </summary>
    public bool IncludeProgress { get; set; } = true;
}
