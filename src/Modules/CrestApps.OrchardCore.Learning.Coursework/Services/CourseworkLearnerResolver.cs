using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Coursework.Services;

/// <summary>
/// Finds the signed-in learner's own enrollment for a coursework item (the open one, else the newest that ended), from
/// the course the item belongs to. Another learner's enrollment is never used.
/// </summary>
public sealed class CourseworkLearnerResolver
{
    private readonly ICourseworkContentSource _contentSource;
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnrollmentStore _enrollments;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseworkLearnerResolver"/> class.
    /// </summary>
    /// <param name="contentSource">Reads the coursework item.</param>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="enrollments">The enrollment store.</param>
    public CourseworkLearnerResolver(ICourseworkContentSource contentSource, ICourseStructureService courseStructure, IEnrollmentStore enrollments)
    {
        _contentSource = contentSource;
        _courseStructure = courseStructure;
        _enrollments = enrollments;
    }

    /// <summary>
    /// Finds a learner's enrollment for a published coursework item.
    /// </summary>
    /// <param name="userId">The signed-in learner.</param>
    /// <param name="courseworkId">The coursework item.</param>
    /// <returns>The item, its course and the learner's enrollment; <see langword="null"/> when there is no such coursework.</returns>
    public async Task<CourseworkLearner> ResolveAsync(string userId, string courseworkId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseworkId))
        {
            return null;
        }

        return await ResolveAsync(userId, await _contentSource.GetPublishedAsync(courseworkId));
    }

    /// <summary>
    /// Finds a learner's enrollment for a coursework item.
    /// </summary>
    /// <param name="userId">The signed-in learner.</param>
    /// <param name="coursework">The coursework item.</param>
    /// <returns>The item, its course and the learner's enrollment; <see langword="null"/> when it is not coursework.</returns>
    public async Task<CourseworkLearner> ResolveAsync(string userId, ContentItem coursework)
    {
        if (string.IsNullOrEmpty(userId) || coursework is null || !coursework.Has<CourseworkPart>())
        {
            return null;
        }

        var courseId = _courseStructure.GetCourseId(coursework);

        if (string.IsNullOrEmpty(courseId))
        {
            return new CourseworkLearner(coursework, null, null);
        }

        // Newest first; the user id is checked again so another learner's enrollment is never used. Without an open
        // one, the newest that ended still shows the learner their work and grade.
        var own = (await _enrollments.ListByCourseForUserAsync(userId, courseId))
            .Where(candidate => string.Equals(candidate.UserId, userId, StringComparison.Ordinal) &&
                string.Equals(candidate.CourseId, courseId, StringComparison.Ordinal))
            .ToArray();
        var enrollment = own.FirstOrDefault(candidate => candidate.IsOpen) ?? own.FirstOrDefault();

        return new CourseworkLearner(coursework, courseId, enrollment);
    }
}

/// <summary>
/// A learner and a coursework item.
/// </summary>
/// <param name="Coursework">The coursework item.</param>
/// <param name="CourseId">The course of the item.</param>
/// <param name="Enrollment">The learner's open enrollment in the course, else the newest that ended, or <see langword="null"/>.</param>
public sealed record CourseworkLearner(ContentItem Coursework, string CourseId, Enrollment Enrollment);
