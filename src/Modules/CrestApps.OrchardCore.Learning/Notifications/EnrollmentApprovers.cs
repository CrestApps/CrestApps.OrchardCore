using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Notifications;

/// <summary>
/// Finds who is told that someone asked to enroll in a course that needs approval: the course's instructors and
/// assistants, who may manage its enrollments, or, for a course without them, the people whose roles grant Manage
/// enrollments, and the administrators.
/// </summary>
internal sealed class EnrollmentApprovers
{
    private static readonly string[] _approvingStaffRoles = [LearningConstants.CourseStaffRoles.Instructor, LearningConstants.CourseStaffRoles.Assistant];

    private readonly IServiceProvider _services;

    public EnrollmentApprovers(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// Lists the user ids of the people who may approve a request to enroll in a course.
    /// </summary>
    /// <param name="course">The course, or <see langword="null"/> when it is gone.</param>
    /// <returns>The user ids, each once.</returns>
    public async Task<IReadOnlyList<string>> ListAsync(ContentItem course)
    {
        var staff = (course?.Get<CoursePart>(nameof(CoursePart))?.Staff ?? [])
            .Where(member => _approvingStaffRoles.Contains(member.Role, StringComparer.Ordinal) && !string.IsNullOrEmpty(member.UserId))
            .Select(member => member.UserId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (staff.Length > 0)
        {
            return staff;
        }

        if (_services.GetService<IRoleService>() is not { } roleService || _services.GetService<UserManager<IUser>>() is not { } userManager)
        {
            return [];
        }

        // The permission-holders lookup of Approvals' permission approver rule; see the Learning remaining-work list.
        var roles = new List<string> { OrchardCoreConstants.Roles.Administrator };

        foreach (var role in await roleService.GetRolesAsync())
        {
            if (role is Role editable && editable.RoleClaims?.Any(claim => claim.ClaimType == Permission.ClaimType && claim.ClaimValue == LearningPermissions.ManageEnrollments.Name) == true)
            {
                roles.Add(role.RoleName);
            }
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var user in await userManager.GetUsersInRoleAsync(role))
            {
                if (await userManager.GetUserIdAsync(user) is { Length: > 0 } id)
                {
                    ids.Add(id);
                }
            }
        }

        return [.. ids];
    }
}
