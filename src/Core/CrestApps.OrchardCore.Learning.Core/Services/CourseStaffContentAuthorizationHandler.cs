using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Gives people with <see cref="LearningPermissions.ManageOwnCourses"/> the content permissions to view, edit, preview
/// and publish a course, and its course items, when they are an instructor or assistant of that course. Deleting stays
/// with the roles that hold the delete permissions.
/// </summary>
public sealed class CourseStaffContentAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private static readonly string[] _editingRoles = [LearningConstants.CourseStaffRoles.Instructor, LearningConstants.CourseStaffRoles.Assistant];

    private static readonly HashSet<string> _granted = new(StringComparer.Ordinal)
    {
        CommonPermissions.ViewContent.Name,
        CommonPermissions.ViewOwnContent.Name,
        CommonPermissions.EditContent.Name,
        CommonPermissions.EditOwnContent.Name,
        CommonPermissions.PublishContent.Name,
        CommonPermissions.PublishOwnContent.Name,
        CommonPermissions.PreviewContent.Name,
        CommonPermissions.PreviewOwnContent.Name,
    };

    private readonly Lazy<ICourseStructureService> _courseStructure;
    private readonly Lazy<IAuthorizationService> _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseStaffContentAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="courseStructure">The course structure reader, resolved lazily.</param>
    /// <param name="authorizationService">The authorization service, resolved lazily.</param>
    public CourseStaffContentAuthorizationHandler(
        Lazy<ICourseStructureService> courseStructure,
        Lazy<IAuthorizationService> authorizationService)
    {
        _courseStructure = courseStructure;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded || context.Resource is not ContentItem item || !_granted.Contains(requirement.Permission.Name) || context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var courseId = item.Has<CoursePart>() ? item.ContentItemId : item.Has<CourseItemPart>() ? _courseStructure.Value.GetCourseId(item) : null;

        if (string.IsNullOrEmpty(courseId))
        {
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (await _courseStructure.Value.IsCourseStaffAsync(userId, courseId, _editingRoles)
            && await _authorizationService.Value.AuthorizeAsync(context.User, LearningPermissions.ManageOwnCourses))
        {
            context.Succeed(requirement);
        }
    }
}
