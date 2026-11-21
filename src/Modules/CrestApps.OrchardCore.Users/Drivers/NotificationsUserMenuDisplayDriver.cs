using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Notifications;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Drivers;

/// <summary>
/// Adds Notifications, the link to the site's Notifications page, to the user menu on the site's pages, for people with
/// the Notifications module's permission. The admin has the module's own notification center instead.
/// </summary>
public sealed class NotificationsUserMenuDisplayDriver : DisplayDriver<UserMenu>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public NotificationsUserMenuDisplayDriver(IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(UserMenu model, BuildDisplayContext context)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true ||
            !await _authorizationService.AuthorizeAsync(principal, NotificationPermissions.ManageNotifications))
        {
            return null;
        }

        return View("UserMenuItems__Notifications", model)
            .Location(OrchardCoreConstants.DisplayType.Detail, "Content:3.8")
            .Differentiator("UserNotifications");
    }
}
