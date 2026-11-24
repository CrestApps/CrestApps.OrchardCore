using CrestApps.OrchardCore.Commerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell.Descriptor.Models;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.Drivers;

/// <summary>
/// Adds My credentials to the user menu on the site's pages when the storefront account is off; with it on, My
/// credentials is an entry of the account menu instead.
/// </summary>
internal sealed class CredentialsUserMenuDisplayDriver : DisplayDriver<UserMenu>
{
    private readonly ShellDescriptor _shellDescriptor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialsUserMenuDisplayDriver"/> class.
    /// </summary>
    /// <param name="shellDescriptor">The enabled features.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public CredentialsUserMenuDisplayDriver(
        ShellDescriptor shellDescriptor,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _shellDescriptor = shellDescriptor;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(UserMenu model, BuildDisplayContext context)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (_shellDescriptor.Features.Any(feature => feature.Id == CommerceConstants.Features.Area)
            || principal is null
            || !await _authorizationService.AuthorizeAsync(principal, LearningPermissions.AccessLearning))
        {
            return null;
        }

        return View("UserMenuItems__Credentials", model)
            .Location("Detail", "Content:3.5")
            .Differentiator("LearningCredentials");
    }
}
