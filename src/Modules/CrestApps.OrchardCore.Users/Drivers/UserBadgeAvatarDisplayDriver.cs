using CrestApps.OrchardCore.Users.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Theming;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Users.Drivers;

public sealed class UserBadgeAvatarDisplayDriver : DisplayDriver<UserBadgeContext>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IThemeManager _themeManager;
    private readonly IAdminThemeService _adminThemeService;

    public UserBadgeAvatarDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IThemeManager themeManager,
        IAdminThemeService adminThemeService)
    {
        _httpContextAccessor = httpContextAccessor;
        _themeManager = themeManager;
        _adminThemeService = adminThemeService;
    }

    public override async Task<IDisplayResult> DisplayAsync(UserBadgeContext model, BuildDisplayContext context)
    {
        var results = new List<IDisplayResult>()
        {
            View("UserBadgeAvatar", model)
            .Location("Summary", "Header:before")
            .Location("AdminSummary", "Header:before")
        };

        if (!model.DisplayUser.TryGet<UserAvatarPart>(out var mediaPart) ||
            mediaPart.Avatar.Paths == null ||
            mediaPart.Avatar.Paths.Length == 0)
        {
            results.Add(View("UserBadgeAvatarMetaIcon", model).Location("AdminSummary", "Header:before"));

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
                results.Add(View("UserBadgeAvatarDefaultIcon", model).Location("Summary", "Header:before"));
            }
        }

        return Combine(results);
    }
}
