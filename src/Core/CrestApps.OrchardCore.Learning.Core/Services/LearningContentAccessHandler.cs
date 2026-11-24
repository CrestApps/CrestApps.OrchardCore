using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Applies the course access decision to Orchard Core's <c>ViewContent</c> on course items. It succeeds for the course
/// staff, authors, free previews and enrolled learners, and otherwise fails the requirement, because a role that holds
/// the general <c>ViewContent</c> permission would otherwise read any lesson. Orchard Core checks this permission on the
/// display route, the content API, GraphQL and, with Secure Media, on every attached file, so all of them follow it.
/// </summary>
public sealed class LearningContentAccessHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly Lazy<ICourseAccessService> _courseAccessService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningContentAccessHandler"/> class.
    /// </summary>
    /// <param name="courseAccessService">The course access decision, resolved lazily because it uses the authorization service.</param>
    public LearningContentAccessHandler(Lazy<ICourseAccessService> courseAccessService)
    {
        _courseAccessService = courseAccessService;
    }

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.Resource is not ContentItem item || !IsViewPermission(requirement))
        {
            return;
        }

        var access = await _courseAccessService.Value.GetItemAccessAsync(context.User, item);

        if (access.Reason == CourseItemAccessReason.NotACourseItem)
        {
            return;
        }

        if (access.Allowed)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, "The person may not open this course item."));
        }
    }

    private static bool IsViewPermission(PermissionRequirement requirement)
        => requirement.Permission.Name == CommonPermissions.ViewContent.Name || requirement.Permission.Name == CommonPermissions.ViewOwnContent.Name;
}
