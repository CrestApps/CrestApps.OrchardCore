using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// A tool that changes a timesheet's punches: like the timesheet page's punch forms, it needs both
/// <see cref="WorkforcePermissions.ApproveTimesheets"/> and <see cref="WorkforcePermissions.EditPunches"/>.
/// </summary>
internal abstract class TimesheetPunchToolBase : WorkforceAdminToolBase
{
    protected override Permission Permission => WorkforcePermissions.EditPunches;

    protected override string NotAllowedMessage
        => $"The user isn't allowed to do this: it needs the '{WorkforcePermissions.ApproveTimesheets.Name}' and '{WorkforcePermissions.EditPunches.Name}' permissions.";

    protected override async Task<bool> IsAllowedAsync(IAuthorizationService authorizationService, ClaimsPrincipal user)
        => await authorizationService.AuthorizeAsync(user, WorkforcePermissions.ApproveTimesheets)
        && await authorizationService.AuthorizeAsync(user, WorkforcePermissions.EditPunches);
}
