using CrestApps.OrchardCore.Users.Core.Models;
using CrestApps.OrchardCore.Users.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Theming;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Users.Drivers;

public sealed class UserBadgeNameDisplayDriver : DisplayDriver<UserBadgeContext>
{
    private readonly DisplayUserOptions _displayUserOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAdminThemeService _adminThemeService;
    private readonly IThemeManager _themeManager;

    public UserBadgeNameDisplayDriver(
        IOptions<DisplayUserOptions> displayUserOptions,
        IHttpContextAccessor httpContextAccessor,
        IAdminThemeService adminThemeService,
        IThemeManager themeManager)
    {
        _displayUserOptions = displayUserOptions.Value;
        _httpContextAccessor = httpContextAccessor;
        _adminThemeService = adminThemeService;
        _themeManager = themeManager;
    }

    public override async Task<IDisplayResult> DisplayAsync(UserBadgeContext model, BuildDisplayContext context)
    {
        if (!_displayUserOptions.ConvertAuthorToShape)
        {
            return null;
        }

        if (context.DisplayType == "Summary")
        {
            var isAdmin = AdminAttribute.IsApplied(_httpContextAccessor.HttpContext);

            if (!isAdmin)
            {
                // Even if the Admin attribute is not applied we might be using the admin theme, for instance in Login views.
                // In this case don't render Layers.
                var selectedTheme = (await _themeManager.GetThemeAsync())?.Id;
                if (!string.IsNullOrEmpty(selectedTheme))
                {
                    var adminTheme = await _adminThemeService.GetAdminThemeNameAsync();
                    isAdmin = selectedTheme == adminTheme;
                }
            }

            if (isAdmin)
            {
                return View("UserBadgeNameAdmin", model).Location("Summary", "Header");
            }

            return View("UserBadgeName", model).Location("Summary", "Header");
        }

        return Combine(
            View("UserBadgeNameContentMetaIcon", model)
            .Location("AdminSummary", "Header:before"),

            View("UserBadgeNameContentMeta", model)
            .Location("AdminSummary", "Header")
        );
    }
}
