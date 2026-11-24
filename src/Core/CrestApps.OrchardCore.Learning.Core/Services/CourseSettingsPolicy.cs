using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Decides who may change the settings of a course that grant access to it: its staff, its enrollment mode and its
/// enrollment key. Editing a course is not enough, because these settings decide who reads learner records and who gets in.
/// </summary>
public sealed class CourseSettingsPolicy
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICourseStructureService _courseStructure;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseSettingsPolicy"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="courseStructure">The course structure.</param>
    public CourseSettingsPolicy(IAuthorizationService authorizationService, ICourseStructureService courseStructure)
    {
        _authorizationService = authorizationService;
        _courseStructure = courseStructure;
    }

    /// <summary>
    /// Gets whether a person may change the staff, the enrollment mode and the key of a course: a course manager, or an
    /// instructor of the published course who holds Manage own courses.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="courseId">The course.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public async Task<bool> CanChangeEnrollmentSettingsAsync(ClaimsPrincipal user, string courseId)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageCourses))
        {
            return true;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return !string.IsNullOrEmpty(courseId)
            && await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageOwnCourses)
            && await _courseStructure.IsCourseStaffAsync(userId, courseId, LearningConstants.CourseStaffRoles.Instructor);
    }
}
