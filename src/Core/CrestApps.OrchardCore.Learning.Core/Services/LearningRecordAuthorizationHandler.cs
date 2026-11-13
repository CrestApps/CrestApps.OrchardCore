using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Narrows the broad Learning permissions to the record: a learner may read their own records, the staff of a course may
/// read the records of its learners, and its instructors and assistants may also manage its enrollments. Callers check
/// the broad permission with the enrollment as the resource; a role that holds the permission passes as before.
/// </summary>
public sealed class LearningRecordAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private static readonly string[] _managingRoles = [LearningConstants.CourseStaffRoles.Instructor, LearningConstants.CourseStaffRoles.Assistant];

    private readonly Lazy<ICourseStructureService> _courseStructure;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningRecordAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="courseStructure">The course structure reader, resolved lazily because it builds the content manager, whose handlers may use authorization.</param>
    public LearningRecordAuthorizationHandler(Lazy<ICourseStructureService> courseStructure)
    {
        _courseStructure = courseStructure;
    }

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded || context.Resource is not Enrollment enrollment || context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var name = requirement.Permission.Name;

        if (name == LearningPermissions.ViewLearnerRecords.Name && enrollment.UserId == userId)
        {
            context.Succeed(requirement);

            return;
        }

        if (name == LearningPermissions.ViewLearnerRecords.Name || name == LearningPermissions.ViewEnrollments.Name)
        {
            if (await _courseStructure.Value.IsCourseStaffAsync(userId, enrollment.CourseId))
            {
                context.Succeed(requirement);
            }

            return;
        }

        if (name == LearningPermissions.ManageEnrollments.Name && await _courseStructure.Value.IsCourseStaffAsync(userId, enrollment.CourseId, _managingRoles))
        {
            context.Succeed(requirement);
        }
    }
}
