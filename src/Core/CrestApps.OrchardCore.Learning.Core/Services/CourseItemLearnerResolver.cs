using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Finds a learner's own enrollment for a course item: the course the item belongs to, and the learner's open
/// enrollment in it, else the newest that ended. Another learner's enrollment is never used. Course item kinds whose
/// learner panel needs the learner's enrollment (packages; coursework and classes keep their own copy for now) use it.
/// </summary>
public sealed class CourseItemLearnerResolver
{
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnrollmentStore _enrollments;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseItemLearnerResolver"/> class.
    /// </summary>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="enrollments">The enrollment store.</param>
    public CourseItemLearnerResolver(ICourseStructureService courseStructure, IEnrollmentStore enrollments)
    {
        _courseStructure = courseStructure;
        _enrollments = enrollments;
    }

    /// <summary>
    /// Finds a learner's enrollment for a course item.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="item">The course item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The course id and the enrollment; both <see langword="null"/> when the item has no course, and the
    /// enrollment <see langword="null"/> when the learner has none in it.</returns>
    public async Task<(string CourseId, Enrollment Enrollment)> ResolveAsync(string userId, ContentItem item, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || item is null)
        {
            return (null, null);
        }

        var courseId = _courseStructure.GetCourseId(item);

        if (string.IsNullOrEmpty(courseId))
        {
            return (null, null);
        }

        // Newest first; the user id and course are checked again so another learner's enrollment is never used.
        var own = (await _enrollments.ListByCourseForUserAsync(userId, courseId, cancellationToken))
            .Where(candidate => string.Equals(candidate.UserId, userId, StringComparison.Ordinal) &&
                string.Equals(candidate.CourseId, courseId, StringComparison.Ordinal))
            .ToArray();

        return (courseId, own.FirstOrDefault(candidate => candidate.IsOpen) ?? own.FirstOrDefault());
    }
}
