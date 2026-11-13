using System.Security.Claims;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Admin;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Workforce.Portal;

/// <summary>
/// Lets a worker into the admin, where the My Work pages are, when they may use My Work: Access admin panel is granted
/// to a signed-in user with a worker profile that isn't terminated and the Use My Work permission. They see only the
/// menu items and pages their other permissions allow.
/// </summary>
internal sealed class WorkerAdminAccessAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IStaffMemberManager _workerManager;
    private readonly Lazy<IAuthorizationService> _authorizationService;

    public WorkerAdminAccessAuthorizationHandler(IStaffMemberManager workerManager, Lazy<IAuthorizationService> authorizationService)
    {
        _workerManager = workerManager;
        _authorizationService = authorizationService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded ||
            !string.Equals(requirement.Permission?.Name, AdminPermissions.AccessAdminPanel.Name, StringComparison.Ordinal) ||
            context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId)
            || await _workerManager.FindByUserIdAsync(userId) is not { } worker
            || worker.Status == StaffStatus.Terminated)
        {
            return;
        }

        if (await _authorizationService.Value.AuthorizeAsync(context.User, WorkforcePermissions.UseWorkforcePortal))
        {
            context.Succeed(requirement);
        }
    }
}
