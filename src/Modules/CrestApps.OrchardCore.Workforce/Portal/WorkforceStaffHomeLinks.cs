using System.Security.Claims;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Portal;

/// <summary>
/// Puts My Work and the schedule on a staff member's own page.
/// </summary>
internal sealed class WorkforceStaffHomeLinks : IStaffHomeLinkProvider
{
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public WorkforceStaffHomeLinks(IAuthorizationService authorizationService, IStringLocalizer<WorkforceStaffHomeLinks> stringLocalizer)
    {
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public async Task<IEnumerable<StaffHomeLink>> GetLinksAsync(StaffMember staffMember, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (!await _authorizationService.AuthorizeAsync(user, WorkforcePermissions.UseWorkforcePortal))
        {
            return [];
        }

        return
        [
            new StaffHomeLink(S["My Work"], "~/Admin/my-work", S["Your shifts, time off, inbox and settings."], "fa-solid fa-briefcase", 10),
            new StaffHomeLink(S["My schedule"], "~/Admin/my-work/schedule", S["The shifts you are scheduled for."], "fa-solid fa-calendar-days", 11),
        ];
    }
}
