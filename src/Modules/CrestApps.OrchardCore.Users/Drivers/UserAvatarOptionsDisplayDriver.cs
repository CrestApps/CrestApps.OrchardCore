using CrestApps.OrchardCore.Users.Core;
using CrestApps.OrchardCore.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Users.Drivers;

/// <summary>
/// Display driver for the user avatar options shape.
/// </summary>
public sealed class UserAvatarOptionsDisplayDriver : SiteDisplayDriver<UserAvatarOptions>
{
    public const string GroupId = "avatarOptions";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellReleaseManager _shellReleaseManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAvatarOptionsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="shellReleaseManager">The shell release manager.</param>
    public UserAvatarOptionsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IShellReleaseManager shellReleaseManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _shellReleaseManager = shellReleaseManager;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override IDisplayResult Edit(ISite site, UserAvatarOptions settings, BuildEditorContext context)
    {
        context.AddTenantReloadWarningWrapper();

        return Initialize<UserAvatarOptions>("UserAvatarOptions_Edit", model =>
        {
            model.Required = settings.Required;
            model.UseDefaultStyle = settings.UseDefaultStyle;
        }).Location("Content:5")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, UserPermissions.ManageAvatarSettings));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, UserAvatarOptions settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, UserPermissions.ManageAvatarSettings))
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

        return Edit(site, settings, context);
    }
}
