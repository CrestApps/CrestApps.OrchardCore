using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Asterisk.ViewModels;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Asterisk.Drivers;

/// <summary>
/// Display driver that renders the tenant-configured Asterisk provider settings tab on the telephony settings screen.
/// </summary>
public sealed class AsteriskSettingsDisplayDriver : SiteDisplayDriver<AsteriskSettings>
{
    private readonly IShellReleaseManager _shellReleaseManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId
        => TelephonyConstants.SettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="shellReleaseManager">The shell release manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AsteriskSettingsDisplayDriver(
        IShellReleaseManager shellReleaseManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        INotifier notifier,
        IHtmlLocalizer<AsteriskSettingsDisplayDriver> htmlLocalizer,
        IStringLocalizer<AsteriskSettingsDisplayDriver> stringLocalizer)
    {
        _shellReleaseManager = shellReleaseManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, AsteriskSettings settings, BuildEditorContext context)
    {
        return Initialize<AsteriskSettingsViewModel>("AsteriskSettings_Edit", model =>
        {
            model.IsEnabled = settings.IsEnabled;
            model.BaseUrl = settings.BaseUrl;
            model.UserName = settings.UserName;
            model.ApplicationName = settings.ApplicationName;
            model.EndpointTemplate = settings.EndpointTemplate;
            model.OutboundCallerId = settings.OutboundCallerId;
            model.TimeoutSeconds = settings.TimeoutSeconds > 0
                ? settings.TimeoutSeconds
                : AsteriskConstants.DefaultTimeoutSeconds;
            model.HasPassword = !string.IsNullOrEmpty(settings.Password);
        }).Location("Content:10#Asterisk")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TelephonyPermissions.ManageTelephonySettings))
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, AsteriskSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, TelephonyPermissions.ManageTelephonySettings))
        {
            return null;
        }

        var model = new AsteriskSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var hasChanges = settings.IsEnabled != model.IsEnabled;
        var telephonySettings = site.GetOrCreate<TelephonySettings>();

        if (!model.IsEnabled)
        {
            if (hasChanges && telephonySettings.DefaultProviderName == AsteriskConstants.ProviderTechnicalName)
            {
                await _notifier.WarningAsync(H["You have disabled the default telephony provider. The soft phone is now disabled until you designate a new default provider."]);

                telephonySettings.DefaultProviderName = null;

                site.Put(telephonySettings);
            }

            settings.IsEnabled = false;
        }
        else
        {
            settings.IsEnabled = true;

            var normalizedBaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl(model.BaseUrl);
            var applicationName = string.IsNullOrWhiteSpace(model.ApplicationName)
                ? AsteriskConstants.DefaultApplicationName
                : model.ApplicationName.Trim();
            var timeoutSeconds = model.TimeoutSeconds > 0
                ? model.TimeoutSeconds
                : AsteriskConstants.DefaultTimeoutSeconds;

            hasChanges |= settings.BaseUrl != normalizedBaseUrl;
            hasChanges |= settings.UserName != model.UserName?.Trim();
            hasChanges |= settings.ApplicationName != applicationName;
            hasChanges |= settings.EndpointTemplate != model.EndpointTemplate?.Trim();
            hasChanges |= settings.OutboundCallerId != model.OutboundCallerId?.Trim();
            hasChanges |= settings.TimeoutSeconds != timeoutSeconds;

            settings.BaseUrl = normalizedBaseUrl;
            settings.UserName = model.UserName?.Trim();
            settings.ApplicationName = applicationName;
            settings.EndpointTemplate = model.EndpointTemplate?.Trim();
            settings.OutboundCallerId = model.OutboundCallerId?.Trim();
            settings.TimeoutSeconds = timeoutSeconds;

            if (string.IsNullOrWhiteSpace(settings.BaseUrl) || !Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.BaseUrl), S["Enter a valid absolute Asterisk ARI URL."]);
            }

            if (string.IsNullOrWhiteSpace(settings.UserName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserName), S["Enter the Asterisk ARI user name."]);
            }

            if (string.IsNullOrWhiteSpace(settings.ApplicationName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApplicationName), S["Enter the Asterisk Stasis application name."]);
            }

            if (settings.TimeoutSeconds <= 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.TimeoutSeconds), S["Enter a timeout greater than zero."]);
            }

            if (string.IsNullOrEmpty(settings.Password) && string.IsNullOrWhiteSpace(model.Password))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Password), S["Enter the Asterisk ARI password."]);
            }

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var protector = _dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName);
                var protectedPassword = protector.Protect(model.Password);

                hasChanges |= settings.Password != protectedPassword;

                settings.Password = protectedPassword;
            }
        }

        if (context.Updater.ModelState.IsValid && settings.IsEnabled && string.IsNullOrEmpty(telephonySettings.DefaultProviderName))
        {
            telephonySettings.DefaultProviderName = AsteriskConstants.ProviderTechnicalName;

            site.Put(telephonySettings);

            hasChanges = true;
        }

        if (hasChanges)
        {
            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }
}
