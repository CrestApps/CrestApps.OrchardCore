using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Classes.Services;

/// <summary>
/// Decides who manages the sessions, rosters and attendance of a class item: everyone with Manage learning classes, or
/// the instructors and assistants on the staff of the class's course who can manage the courses they teach.
/// </summary>
public sealed class ClassesAdminScope
{
    private static readonly string[] _staffRoles =
    [
        LearningConstants.CourseStaffRoles.Instructor,
        LearningConstants.CourseStaffRoles.Assistant,
    ];

    private readonly IAuthorizationService _authorizationService;
    private readonly ICourseStructureService _courseStructure;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassesAdminScope"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="courseStructure">The course structure reader.</param>
    public ClassesAdminScope(IAuthorizationService authorizationService, ICourseStructureService courseStructure)
    {
        _authorizationService = authorizationService;
        _courseStructure = courseStructure;
    }

    /// <summary>
    /// Gets whether a person manages the sessions of a class item.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="classItem">The class course item.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public async Task<bool> CanManageAsync(ClaimsPrincipal user, ContentItem classItem)
    {
        if (user is null || classItem is null)
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(user, LearningClassesPermissions.ManageLearningClasses))
        {
            return true;
        }

        // Being named on a course is not enough: course staff manage classes only with a Learning permission too, as for grading.
        if (!await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageOwnCourses))
        {
            return false;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var courseId = _courseStructure.GetCourseId(classItem);

        return !string.IsNullOrEmpty(userId) &&
            !string.IsNullOrEmpty(courseId) &&
            await _courseStructure.IsCourseStaffAsync(userId, courseId, _staffRoles);
    }
}
