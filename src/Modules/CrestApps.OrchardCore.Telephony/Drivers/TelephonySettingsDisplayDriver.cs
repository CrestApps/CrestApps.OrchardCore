using CrestApps.OrchardCore.Telephony.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telephony.Drivers;

/// <summary>
/// Display driver that renders the default telephony provider selector tab on the telephony settings screen.
/// </summary>
public sealed class TelephonySettingsDisplayDriver : SiteDisplayDriver<TelephonySettings>
{
    private readonly IShellReleaseManager _shellReleaseManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly TelephonyProviderOptions _providerOptions;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId
        => TelephonyConstants.SettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonySettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="shellReleaseManager">The shell release manager used to apply provider changes.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="providerOptions">The registered telephony provider options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TelephonySettingsDisplayDriver(
        IShellReleaseManager shellReleaseManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IOptions<TelephonyProviderOptions> providerOptions,
        IStringLocalizer<TelephonySettingsDisplayDriver> stringLocalizer)
    {
        _shellReleaseManager = shellReleaseManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _providerOptions = providerOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, TelephonySettings settings, BuildEditorContext context)
    {
        return Initialize<TelephonySettingsViewModel>("TelephonySettings_Edit", model =>
        {
            model.DefaultProvider = settings.DefaultProviderName;
            model.Providers = _providerOptions.Providers
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

        if (settings.DefaultProviderName != model.DefaultProvider)
        {
            settings.DefaultProviderName = model.DefaultProvider;

            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }
}
