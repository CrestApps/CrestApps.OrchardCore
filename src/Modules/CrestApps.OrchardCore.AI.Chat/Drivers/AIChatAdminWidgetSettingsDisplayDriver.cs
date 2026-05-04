using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Chat.Settings;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for the AI chat admin widget settings shape.
/// </summary>
public sealed class AIChatAdminWidgetSettingsDisplayDriver : SiteDisplayDriver<AIChatAdminWidgetSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIProfileManager _profileManager;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatAdminWidgetSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="profileManager">The profile manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIChatAdminWidgetSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        IStringLocalizer<AIChatAdminWidgetSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _profileManager = profileManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, AIChatAdminWidgetSettings settings, BuildEditorContext context)
    {
        return Initialize<AIChatAdminWidgetSettingsViewModel>("AIChatAdminWidgetSettings_Edit", async model =>
        {
            model.ProfileId = settings.ProfileId;
            model.MaxSessions = settings.MaxSessions;
            model.PrimaryColor = settings.PrimaryColor;

            var profiles = await _profileManager.GetAsync(AIProfileType.Chat);
            model.Profiles = profiles
                .OrderBy(p => p.DisplayText ?? p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => new SelectListItem(p.DisplayText ?? p.Name, p.ItemId));
        })
        .Location("Content:7%Admin Widget;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, AIPermissions.ManageAIProfiles));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, AIChatAdminWidgetSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, AIPermissions.ManageAIProfiles))
        {
            return null;
        }

        var model = new AIChatAdminWidgetSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.ProfileId = string.IsNullOrWhiteSpace(model.ProfileId) ? null : model.ProfileId;
        settings.MaxSessions = Math.Clamp(
            model.MaxSessions,
            AIChatAdminWidgetSettings.MinMaxSessions,
            AIChatAdminWidgetSettings.MaxMaxSessions);
        settings.PrimaryColor = string.IsNullOrWhiteSpace(model.PrimaryColor)
            ? AIChatAdminWidgetSettings.DefaultPrimaryColor
            : model.PrimaryColor.Trim();

        return Edit(site, settings, context);
    }
}
