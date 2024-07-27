using CrestApps.OrchardCore.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Users.Drivers;

public sealed class UserAvatarOptionsDisplayDriver : SectionDisplayDriver<ISite, UserAvatarOptions>
{
    public const string GroupId = "avatarOptions";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellReleaseManager _shellReleaseManager;

    public UserAvatarOptionsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IShellReleaseManager shellReleaseManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _shellReleaseManager = shellReleaseManager;
    }

    public override async Task<IDisplayResult> EditAsync(UserAvatarOptions settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, UserPermissions.ManageAvatarSettings))
        {
            return null;
        }

        return Initialize<UserAvatarOptions>("UserAvatarOptions_Edit", model =>
        {
            model.Required = settings.Required;
            model.UseDefaultStyle = settings.UseDefaultStyle;
        }).Location("Content:5")
        .OnGroup(GroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(UserAvatarOptions settings, UpdateEditorContext context)
    {
        if (!context.GroupId.Equals(GroupId, StringComparison.OrdinalIgnoreCase) ||
            !await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, UserPermissions.ManageAvatarSettings))
        {
            return null;
        }

        var model = new UserAvatarOptions();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.Required = model.Required;
        settings.UseDefaultStyle |= model.UseDefaultStyle;

        if (model.Required != settings.Required || model.UseDefaultStyle != settings.UseDefaultStyle)
        {
            _shellReleaseManager.RequestRelease();
        }

        return await EditAsync(settings, context);
    }
}
