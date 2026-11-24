using System.Security.Claims;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Navigation;

/// <summary>
/// Puts My learning on a staff member's own page.
/// </summary>
internal sealed class LearningStaffHomeLinks : IStaffHomeLinkProvider
{
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public LearningStaffHomeLinks(IAuthorizationService authorizationService, IStringLocalizer<LearningStaffHomeLinks> stringLocalizer)
    {
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public async Task<IEnumerable<StaffHomeLink>> GetLinksAsync(StaffMember staffMember, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => await _authorizationService.AuthorizeAsync(user, LearningPermissions.AccessLearning)
            ? [new StaffHomeLink(S["My learning"], "~/learning", S["The courses you are taking and your transcript."], "fa-solid fa-graduation-cap", 40)]
            : [];
}
