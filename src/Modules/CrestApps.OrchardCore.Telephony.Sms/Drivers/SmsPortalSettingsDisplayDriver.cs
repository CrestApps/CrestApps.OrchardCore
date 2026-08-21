using CrestApps.OrchardCore.Telephony.Sms.Core;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telephony.Sms.Drivers;

/// <summary>
/// Renders and persists the tenant-default SMS provider on the SMS portal site-settings screen.
/// </summary>
public sealed class SmsPortalSettingsDisplayDriver : SiteDisplayDriver<SmsPortalSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public SmsPortalSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId => TelephonySmsConstants.Settings.GroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, SmsPortalSettings settings, BuildEditorContext context)
    {
        return Initialize<SmsPortalSettings>("SmsPortalSettings_Edit", model =>
            {
                model.DefaultProviderName = settings.DefaultProviderName;
            })
            .Location("Content:1")
            .OnGroup(SettingsGroupId)
            .RenderWhen(() => _authorizationService.AuthorizeAsync(
                _httpContextAccessor.HttpContext?.User,
                TelephonySmsPermissions.ManageSmsNumberRoutes));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, SmsPortalSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(
                _httpContextAccessor.HttpContext?.User,
                TelephonySmsPermissions.ManageSmsNumberRoutes))
        {
            return null;
        }

        var model = new SmsPortalSettings();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.DefaultProviderName = model.DefaultProviderName?.Trim();

        return Edit(site, settings, context);
    }
}
