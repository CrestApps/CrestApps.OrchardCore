using CrestApps.OrchardCore.Telephony.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telephony.Drivers;

/// <summary>
/// Display driver that renders the default telephony provider selector tab on the telephony settings screen.
/// </summary>
public sealed class TelephonySettingsDisplayDriver : SiteDisplayDriver<TelephonySettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IOptionsMonitor<TelephonyProviderOptions> _providerOptions;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId
        => TelephonyConstants.SettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonySettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="providerOptions">The registered telephony provider options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TelephonySettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IOptionsMonitor<TelephonyProviderOptions> providerOptions,
        IStringLocalizer<TelephonySettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _providerOptions = providerOptions;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, TelephonySettings settings, BuildEditorContext context)
    {
        return Initialize<TelephonySettingsViewModel>("TelephonySettings_Edit", model =>
        {
            model.DefaultProvider = settings.DefaultProviderName;
            model.Providers = _providerOptions.CurrentValue.Providers
                .Where(entry => entry.Value.IsEnabled)
                .Select(entry => new SelectListItem(entry.Key, entry.Key))
                .OrderBy(item => item.Text)
                .ToArray();
        }).Location("Content:1#Soft Phone")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TelephonyPermissions.ManageTelephonySettings))
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, TelephonySettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, TelephonyPermissions.ManageTelephonySettings))
        {
            return null;
        }

        var model = new TelephonySettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // The default provider name is read live from the site settings by every consumer
        // (via ISiteService or IOptionsSnapshot<TelephonySettings>), so changing it does not
        // require releasing the shell.
        settings.DefaultProviderName = model.DefaultProvider;

        return Edit(site, settings, context);
    }
}
