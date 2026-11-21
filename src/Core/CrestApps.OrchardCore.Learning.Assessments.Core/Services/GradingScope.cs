using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Decides whose work a person may grade: everyone with Grade assessments, or the graders, instructors and assistants
/// on the staff of the course of the work, who also hold Manage the courses you teach. Nobody grades their own work.
/// Every grading page (quiz attempts, coursework) uses it.
/// </summary>
public sealed class GradingScope
{
    private static readonly string[] _gradingRoles =
    [
        LearningConstants.CourseStaffRoles.Grader,
        LearningConstants.CourseStaffRoles.Instructor,
        LearningConstants.CourseStaffRoles.Assistant,
    ];

    private readonly IAuthorizationService _authorizationService;
    private readonly ICourseStructureService _courseStructure;

    /// <summary>
    /// Initializes a new instance of the <see cref="GradingScope"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="courseStructure">The course structure reader.</param>
    public GradingScope(IAuthorizationService authorizationService, ICourseStructureService courseStructure)
    {
        _authorizationService = authorizationService;
        _courseStructure = courseStructure;
    }

    /// <summary>
    /// Gets the courses a person grades: <see langword="null"/> for every course, an empty list for none.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <returns>The course ids.</returns>
    public async Task<IReadOnlyCollection<string>> GetCourseIdsAsync(ClaimsPrincipal user)
    {
        if (user is null)
        {
            return [];
        }

        if (await _authorizationService.AuthorizeAsync(user, AssessmentPermissions.GradeAssessments))
        {
            return null;
        }

        // Being named on a course is not enough: course staff grade only with a Learning permission too, as for records.
        if (!await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageOwnCourses))
        {
            return [];
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(userId) ? [] : await _courseStructure.GetStaffCourseIdsAsync(userId, _gradingRoles);
    }

    /// <summary>
    /// Gets whether a person may grade an attempt.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="attempt">The attempt.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public Task<bool> CanGradeAsync(ClaimsPrincipal user, AssessmentAttempt attempt)
        => attempt is null ? Task.FromResult(false) : CanGradeAsync(user, attempt.UserId, attempt.CourseId);

    /// <summary>
    /// Gets whether a person may grade the work of a learner in a course.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="learnerId">The learner whose work it is.</param>
    /// <param name="courseId">The course of the work.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public async Task<bool> CanGradeAsync(ClaimsPrincipal user, string learnerId, string courseId)
    {
        // Nobody grades, regrades or voids their own work.
        if (user is null || string.Equals(learnerId, user.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal))
        {
            return false;
        }

        var courses = await GetCourseIdsAsync(user);

        return courses is null || (courseId is not null && courses.Contains(courseId));
    }
}
