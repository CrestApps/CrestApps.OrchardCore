using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for anonymous AI chat visitor identity settings.
/// </summary>
public sealed class AIVisitorIdentityOptionsDisplayDriver : SiteDisplayDriver<AIVisitorIdentityOptions>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellReleaseManager _shellReleaseManager;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVisitorIdentityOptionsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="shellReleaseManager">The shell release manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIVisitorIdentityOptionsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IShellReleaseManager shellReleaseManager,
        IStringLocalizer<AIVisitorIdentityOptionsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _shellReleaseManager = shellReleaseManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, AIVisitorIdentityOptions settings, BuildEditorContext context)
    {
        context.AddTenantReloadWarningWrapper();

        return Initialize<AIVisitorIdentityOptionsViewModel>("AIVisitorIdentityOptions_Edit", model =>
        {
            model.CookieName = settings.CookieName;
            model.CookieLifetimeDays = (int)Math.Round(settings.CookieLifetime.TotalDays);
            model.RemoteAddressMode = settings.RemoteAddressMode;
            model.RemoteAddressHashSalt = settings.RemoteAddressHashSalt;
        }).Location("Content:2.1%Visitor Identity;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, AIVisitorIdentityOptions settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return null;
        }

        var model = new AIVisitorIdentityOptionsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.CookieName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CookieName), S["The visitor cookie name is required."]);
        }

        if (model.CookieLifetimeDays < 1 || model.CookieLifetimeDays > 3_650)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CookieLifetimeDays), S["Cookie lifetime must be between {0} and {1} day(s).", 1, 3_650]);
        }

        if (model.RemoteAddressMode is AIVisitorRemoteAddressMode.Hashed or AIVisitorRemoteAddressMode.Encrypted &&
            string.IsNullOrWhiteSpace(model.RemoteAddressHashSalt))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RemoteAddressHashSalt), S["A hash salt is required when remote addresses are hashed or encrypted."]);
        }

        if (!context.Updater.ModelState.IsValid)
        {
            return Edit(site, settings, context);
        }

        var cookieName = model.CookieName.Trim();
        var cookieLifetime = TimeSpan.FromDays(model.CookieLifetimeDays);
        var remoteAddressHashSalt = string.IsNullOrWhiteSpace(model.RemoteAddressHashSalt)
            ? settings.RemoteAddressHashSalt
            : model.RemoteAddressHashSalt.Trim();
        var settingsChanged =
            !string.Equals(settings.CookieName, cookieName, StringComparison.Ordinal) ||
            settings.CookieLifetime != cookieLifetime ||
            settings.RemoteAddressMode != model.RemoteAddressMode ||
            !string.Equals(settings.RemoteAddressHashSalt, remoteAddressHashSalt, StringComparison.Ordinal);

        settings.CookieName = cookieName;
        settings.CookieLifetime = cookieLifetime;
        settings.RemoteAddressMode = model.RemoteAddressMode;
        settings.RemoteAddressHashSalt = remoteAddressHashSalt;

        if (settingsChanged)
        {
            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }
}
