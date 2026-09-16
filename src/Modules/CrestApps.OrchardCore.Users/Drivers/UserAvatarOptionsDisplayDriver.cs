using CrestApps.OrchardCore.Users.Core;
using CrestApps.OrchardCore.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Options;
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
    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAvatarOptionsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="optionsUpdateNotifier">The options update notifier.</param>
    public UserAvatarOptionsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IOptionsUpdateNotifier optionsUpdateNotifier)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _optionsUpdateNotifier = optionsUpdateNotifier;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override IDisplayResult Edit(ISite site, UserAvatarOptions settings, BuildEditorContext context)
    {
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

        var useDefaultStyle = settings.UseDefaultStyle | model.UseDefaultStyle;
        var settingsChanged =
            settings.Required != model.Required ||
            settings.UseDefaultStyle != useDefaultStyle;

        settings.Required = model.Required;
        settings.UseDefaultStyle = useDefaultStyle;

        if (settingsChanged)
        {
            _optionsUpdateNotifier.RequestUpdate<UserAvatarOptions>();
        }

        return Edit(site, settings, context);
    }
}
