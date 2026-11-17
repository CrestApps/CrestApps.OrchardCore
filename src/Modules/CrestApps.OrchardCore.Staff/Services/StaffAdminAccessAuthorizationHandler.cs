using System.Security.Claims;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Admin;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// Lets a staff member into the admin to reach their own pages: My Profile is an admin page, and so are the pages other
/// modules link from it (My Pay, My bank accounts). A user linked to a staff member who is not terminated and may view
/// their own staff profile may open the admin; each page still checks its own permission.
/// </summary>
internal sealed class StaffAdminAccessAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IStaffMemberManager _staff;
    private readonly Lazy<IAuthorizationService> _authorizationService;

    public StaffAdminAccessAuthorizationHandler(IStaffMemberManager staff, Lazy<IAuthorizationService> authorizationService)
    {
        _staff = staff;
        _authorizationService = authorizationService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded
            || !string.Equals(requirement.Permission?.Name, AdminPermissions.AccessAdminPanel.Name, StringComparison.Ordinal)
            || context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId)
            || await _staff.FindByUserIdAsync(userId) is not { } member
            || member.Status == StaffStatus.Terminated)
        {
            return;
        }

        if (await _authorizationService.Value.AuthorizeAsync(context.User, StaffPermissions.ViewOwnStaffProfile))
        {
            context.Succeed(requirement);
        }
    }
}
