using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telephony.Drivers;

/// <summary>
/// Display driver that renders the soft phone widget tab on the telephony settings screen.
/// </summary>
public sealed class SoftPhoneWidgetSettingsDisplayDriver : SiteDisplayDriver<SoftPhoneWidgetSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId
        => TelephonyConstants.SettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftPhoneWidgetSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SoftPhoneWidgetSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<SoftPhoneWidgetSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, SoftPhoneWidgetSettings settings, BuildEditorContext context)
    {
        return Initialize<SoftPhoneWidgetSettingsViewModel>("SoftPhoneWidgetSettings_Edit", model =>
        {
            model.DisplayOnAdmin = settings.DisplayOnAdmin;
            model.DisplayOnFrontend = settings.DisplayOnFrontend;
            model.AccentColor = string.IsNullOrWhiteSpace(settings.AccentColor)
                ? SoftPhoneWidgetSettings.DefaultAccentColor
                : settings.AccentColor;
        }).Location("Content:5#Soft Phone")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TelephonyPermissions.ManageTelephonySettings))
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, SoftPhoneWidgetSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, TelephonyPermissions.ManageTelephonySettings))
        {
            return null;
        }

        var model = new SoftPhoneWidgetSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.DisplayOnAdmin = model.DisplayOnAdmin;
        settings.DisplayOnFrontend = model.DisplayOnFrontend;
        settings.AccentColor = string.IsNullOrWhiteSpace(model.AccentColor)
            ? SoftPhoneWidgetSettings.DefaultAccentColor
            : model.AccentColor.Trim();

        return Edit(site, settings, context);
    }
}
