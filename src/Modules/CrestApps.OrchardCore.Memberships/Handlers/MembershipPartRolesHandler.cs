using System.Security.Claims;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Users.Core.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Memberships.Handlers;

/// <summary>
/// The roles a membership product grants are chosen only by someone who may manage memberships and assign roles to
/// users, as in its editor, also when the product is saved through the content API or an import.
/// </summary>
internal sealed class MembershipPartRolesHandler : GrantedRolesPartHandler<MembershipPart>
{
    private readonly IAuthorizationService _authorizationService;

    public MembershipPartRolesHandler(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<MembershipPartRolesHandler> stringLocalizer)
        : base(httpContextAccessor, authorizationService, stringLocalizer)
    {
        _authorizationService = authorizationService;
    }

    protected override IEnumerable<string> GetRoleNames(MembershipPart part)
        => part.RoleNames;

    protected override async Task<bool> CanChooseRolesAsync(ClaimsPrincipal user)
        => await _authorizationService.AuthorizeAsync(user, MembershipsPermissions.ManageMemberships) &&
            await base.CanChooseRolesAsync(user);
}
