using CrestApps.OrchardCore.Commerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell.Descriptor.Models;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// Adds My learning to the user menu on the site's pages when the storefront account is off; with it on, My learning
/// is an entry of the account menu instead.
/// </summary>
internal sealed class LearningUserMenuDisplayDriver : DisplayDriver<UserMenu>
{
    private readonly ShellDescriptor _shellDescriptor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LearningUserMenuDisplayDriver(
        ShellDescriptor shellDescriptor,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _shellDescriptor = shellDescriptor;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(UserMenu model, BuildDisplayContext context)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (_shellDescriptor.Features.Any(feature => feature.Id == CommerceConstants.Features.Area)
            || principal is null
            || !await _authorizationService.AuthorizeAsync(principal, LearningPermissions.AccessLearning))
        {
            return null;
        }

        return View("UserMenuItems__Learning", model)
            .Location("Detail", "Content:3")
            .Differentiator("Learning");
    }
}
