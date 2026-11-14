using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Learning.Assessments.Services;

/// <summary>
/// Decides which attempts a person may grade: everyone with Grade assessments, or the graders, instructors and
/// assistants on the staff of the course of the attempt.
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
        if (await _authorizationService.AuthorizeAsync(user, AssessmentPermissions.GradeAssessments))
        {
            return null;
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
    public async Task<bool> CanGradeAsync(ClaimsPrincipal user, AssessmentAttempt attempt)
    {
        var courses = await GetCourseIdsAsync(user);

        return courses is null || (attempt.CourseId is not null && courses.Contains(attempt.CourseId));
    }
}
