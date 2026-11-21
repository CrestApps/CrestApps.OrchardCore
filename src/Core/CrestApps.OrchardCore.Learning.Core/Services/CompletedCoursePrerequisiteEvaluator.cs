using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Answers the <c>Course</c> prerequisite: the learner completed the course at least once.
/// </summary>
public sealed class CompletedCoursePrerequisiteEvaluator : ICoursePrerequisiteEvaluator
{
    /// <summary>
    /// The kind of prerequisite.
    /// </summary>
    public const string CourseKind = "Course";

    private readonly IEnrollmentStore _enrollmentStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompletedCoursePrerequisiteEvaluator"/> class.
    /// </summary>
    /// <param name="enrollmentStore">The enrollment store.</param>
    public CompletedCoursePrerequisiteEvaluator(IEnrollmentStore enrollmentStore)
    {
        _enrollmentStore = enrollmentStore;
    }

    /// <inheritdoc />
    public string Kind => CourseKind;

    /// <inheritdoc />
    public async Task<bool> IsMetAsync(string userId, CoursePrerequisite prerequisite, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prerequisite);

        var enrollments = await _enrollmentStore.ListByCourseForUserAsync(userId, prerequisite.Id, cancellationToken);

        return enrollments.Any(enrollment => enrollment.Status == EnrollmentStatus.Completed);
    }
}
