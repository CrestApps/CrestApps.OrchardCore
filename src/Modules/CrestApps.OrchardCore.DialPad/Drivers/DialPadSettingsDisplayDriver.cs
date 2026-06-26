using CrestApps.OrchardCore.DialPad.Models;
using CrestApps.OrchardCore.DialPad.ViewModels;
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

namespace CrestApps.OrchardCore.DialPad.Drivers;

/// <summary>
/// Display driver that renders the DialPad provider settings tab on the telephony settings screen.
/// </summary>
public sealed class DialPadSettingsDisplayDriver : SiteDisplayDriver<DialPadSettings>
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
    /// Initializes a new instance of the <see cref="DialPadSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="shellReleaseManager">The shell release manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialPadSettingsDisplayDriver(
        IShellReleaseManager shellReleaseManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        INotifier notifier,
        IHtmlLocalizer<DialPadSettingsDisplayDriver> htmlLocalizer,
        IStringLocalizer<DialPadSettingsDisplayDriver> stringLocalizer)
    {
        _shellReleaseManager = shellReleaseManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, DialPadSettings settings, BuildEditorContext context)
    {
        return Initialize<DialPadSettingsViewModel>("DialPadSettings_Edit", model =>
        {
            model.IsEnabled = settings.IsEnabled;
            model.AuthenticationType = GetEffectiveAuthenticationType(settings);
            model.ClientId = settings.ClientId;
            model.Scopes = settings.Scopes;
            model.UserId = settings.UserId;
            model.OutboundCallerId = settings.OutboundCallerId;
            model.HasApiToken = !string.IsNullOrEmpty(settings.ApiToken);
            model.HasClientSecret = !string.IsNullOrEmpty(settings.ClientSecret);
        }).Location("Content:10#DialPad")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TelephonyPermissions.ManageTelephonySettings))
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, DialPadSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, TelephonyPermissions.ManageTelephonySettings))
        {
            return null;
        }

        var model = new DialPadSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var hasChanges = settings.IsEnabled != model.IsEnabled;
        var telephonySettings = site.GetOrCreate<TelephonySettings>();

        if (!model.IsEnabled)
        {
            if (hasChanges && telephonySettings.DefaultProviderName == DialPadConstants.ProviderTechnicalName)
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

            hasChanges |= settings.AuthenticationType != model.AuthenticationType;
            hasChanges |= settings.UserId != model.UserId;
            hasChanges |= settings.OutboundCallerId != model.OutboundCallerId;
            hasChanges |= settings.ClientId != model.ClientId;
            hasChanges |= settings.Scopes != model.Scopes;
            settings.AuthenticationType = model.AuthenticationType;
            settings.UserId = model.UserId;
            settings.OutboundCallerId = model.OutboundCallerId;
            settings.ClientId = model.ClientId;
            settings.Scopes = model.Scopes;

            if (!Enum.IsDefined(model.AuthenticationType) || model.AuthenticationType == DialPadAuthenticationType.NotConfigured)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AuthenticationType), S["Select a DialPad authentication type."]);
            }
            if (model.AuthenticationType == DialPadAuthenticationType.OAuth2)
            {
                if (string.IsNullOrWhiteSpace(model.ClientId))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ClientId), S["Enter the OAuth client id issued by DialPad."]);
                }

                if (string.IsNullOrEmpty(settings.ClientSecret) && string.IsNullOrWhiteSpace(model.ClientSecret))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ClientSecret), S["Enter the OAuth client secret issued by DialPad."]);
                }
            }
            else if (model.AuthenticationType == DialPadAuthenticationType.ApiKey)
            {
                if (string.IsNullOrEmpty(settings.ApiToken) && string.IsNullOrWhiteSpace(model.ApiToken))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiToken), S["Enter the DialPad API key."]);
                }

                if (string.IsNullOrWhiteSpace(model.UserId))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserId), S["Enter the DialPad user id that places outbound calls."]);
                }
            }

            if (!string.IsNullOrWhiteSpace(model.ApiToken))
            {
                var protector = _dataProtectionProvider.CreateProtector(DialPadConstants.ProtectorName);
                var protectedToken = protector.Protect(model.ApiToken);

                hasChanges |= settings.ApiToken != protectedToken;

                settings.ApiToken = protectedToken;
            }

            if (!string.IsNullOrWhiteSpace(model.ClientSecret))
            {
                var protector = _dataProtectionProvider.CreateProtector(DialPadConstants.OAuthProtectorName);
                var protectedSecret = protector.Protect(model.ClientSecret);

                hasChanges |= settings.ClientSecret != protectedSecret;

                settings.ClientSecret = protectedSecret;
            }
        }

        if (context.Updater.ModelState.IsValid && settings.IsEnabled && string.IsNullOrEmpty(telephonySettings.DefaultProviderName))
        {
            telephonySettings.DefaultProviderName = DialPadConstants.ProviderTechnicalName;

            site.Put(telephonySettings);

            hasChanges = true;
        }

        if (hasChanges)
        {
            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }

    private static DialPadAuthenticationType GetEffectiveAuthenticationType(DialPadSettings settings)
    {
        if (settings.AuthenticationType != DialPadAuthenticationType.NotConfigured)
        {
            return settings.AuthenticationType;
        }

        if (!string.IsNullOrEmpty(settings.ApiToken))
        {
            return DialPadAuthenticationType.ApiKey;
        }

        if (!string.IsNullOrWhiteSpace(settings.ClientId) || !string.IsNullOrEmpty(settings.ClientSecret))
        {
            return DialPadAuthenticationType.OAuth2;
        }

        return DialPadAuthenticationType.NotConfigured;
    }
}
