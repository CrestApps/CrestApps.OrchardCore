using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Classes.Services;

/// <summary>
/// Finds the signed-in learner's own enrollment for a class item (the open one, else the newest that ended), from the course the item belongs to.
/// </summary>
public sealed class ClassLearnerResolver
{
    private readonly IContentManager _contentManager;
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnrollmentStore _enrollments;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassLearnerResolver"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="enrollments">The enrollment store.</param>
    public ClassLearnerResolver(
        IContentManager contentManager,
        ICourseStructureService courseStructure,
        IEnrollmentStore enrollments)
    {
        _contentManager = contentManager;
        _courseStructure = courseStructure;
        _enrollments = enrollments;
    }

    /// <summary>
    /// Finds a learner's enrollment for a published class item.
    /// </summary>
    /// <param name="userId">The signed-in learner.</param>
    /// <param name="classItemId">The class course item.</param>
    /// <returns>The class item, its course and the learner's enrollment; <see langword="null"/> when there is no such class.</returns>
    public async Task<ClassLearner> ResolveAsync(string userId, string classItemId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(classItemId))
        {
            return null;
        }

        return await ResolveAsync(userId, await _contentManager.GetAsync(classItemId, VersionOptions.Published));
    }

    /// <summary>
    /// Finds a learner's enrollment for a class item.
    /// </summary>
    /// <param name="userId">The signed-in learner.</param>
    /// <param name="classItem">The class course item.</param>
    /// <returns>The class item, its course and the learner's enrollment; <see langword="null"/> when it is not a class.</returns>
    public async Task<ClassLearner> ResolveAsync(string userId, ContentItem classItem)
    {
        if (string.IsNullOrEmpty(userId) || classItem is null || !classItem.Has<ClassPart>())
        {
            return null;
        }

        var courseId = _courseStructure.GetCourseId(classItem);

        if (string.IsNullOrEmpty(courseId))
        {
            return new ClassLearner(classItem, null, null);
        }

        // Newest first; the user id is checked again so another learner's enrollment is never used. The open one
        // counts; without one, the newest that ended still shows the learner their place (attending the last class
        // completes the course), and registration refuses it as not active.
        var own = (await _enrollments.ListByCourseForUserAsync(userId, courseId))
            .Where(candidate => string.Equals(candidate.UserId, userId, StringComparison.Ordinal) &&
                string.Equals(candidate.CourseId, courseId, StringComparison.Ordinal))
            .ToArray();
        var enrollment = own.FirstOrDefault(candidate => candidate.IsOpen) ?? own.FirstOrDefault();

        return new ClassLearner(classItem, courseId, enrollment);
    }
}

/// <summary>
/// A learner and a class item.
/// </summary>
/// <param name="ClassItem">The class course item.</param>
/// <param name="CourseId">The course of the class item.</param>
/// <param name="Enrollment">The learner's newest open enrollment in the course, or <see langword="null"/> when they are not enrolled.</param>
public sealed record ClassLearner(ContentItem ClassItem, string CourseId, Enrollment Enrollment);
