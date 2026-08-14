using CrestApps.OrchardCore.Dialpad.Models;
using CrestApps.OrchardCore.Dialpad.ViewModels;
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

namespace CrestApps.OrchardCore.Dialpad.Drivers;

/// <summary>
/// Display driver that renders the Dialpad provider settings tab on the telephony settings screen.
/// </summary>
public sealed class DialpadSettingsDisplayDriver : SiteDisplayDriver<DialpadSettings>
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
    /// Initializes a new instance of the <see cref="DialpadSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="shellReleaseManager">The shell release manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialpadSettingsDisplayDriver(
        IShellReleaseManager shellReleaseManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        INotifier notifier,
        IHtmlLocalizer<DialpadSettingsDisplayDriver> htmlLocalizer,
        IStringLocalizer<DialpadSettingsDisplayDriver> stringLocalizer)
    {
        _shellReleaseManager = shellReleaseManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, DialpadSettings settings, BuildEditorContext context)
    {
        return Initialize<DialpadSettingsViewModel>("DialpadSettings_Edit", model =>
        {
            model.IsEnabled = settings.IsEnabled;
            model.Environment = settings.Environment;
            model.AuthenticationType = GetEffectiveAuthenticationType(settings);
            model.ClientId = settings.ClientId;
            model.Scopes = settings.Scopes;
            model.UserId = settings.UserId;
            model.OutboundCallerId = settings.OutboundCallerId;
            model.HasApiToken = !string.IsNullOrEmpty(settings.ApiToken);
            model.HasClientSecret = !string.IsNullOrEmpty(settings.ClientSecret);
            model.HasWebhookSigningSecret = !string.IsNullOrEmpty(settings.WebhookSigningSecret);
        }).Location("Content:10#Dialpad")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TelephonyPermissions.ManageTelephonySettings))
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, DialpadSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, TelephonyPermissions.ManageTelephonySettings))
        {
            return null;
        }

        var model = new DialpadSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var hasChanges = settings.IsEnabled != model.IsEnabled;
        var telephonySettings = site.GetOrCreate<TelephonySettings>();

        if (!model.IsEnabled)
        {
            if (hasChanges && telephonySettings.DefaultProviderName == DialpadConstants.ProviderTechnicalName)
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

            hasChanges |= settings.Environment != model.Environment;
            hasChanges |= settings.AuthenticationType != model.AuthenticationType;
            hasChanges |= settings.UserId != model.UserId;
            hasChanges |= settings.OutboundCallerId != model.OutboundCallerId;
            hasChanges |= settings.ClientId != model.ClientId;
            hasChanges |= settings.Scopes != model.Scopes;
            settings.Environment = model.Environment;
            settings.AuthenticationType = model.AuthenticationType;
            settings.UserId = model.UserId;
            settings.OutboundCallerId = model.OutboundCallerId;
            settings.ClientId = model.ClientId;
            settings.Scopes = model.Scopes;

            if (!Enum.IsDefined(model.AuthenticationType) || model.AuthenticationType == DialpadAuthenticationType.NotConfigured)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AuthenticationType), S["Select a Dialpad authentication type."]);
            }
            if (model.AuthenticationType == DialpadAuthenticationType.OAuth2)
            {
                if (string.IsNullOrWhiteSpace(model.ClientId))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ClientId), S["Enter the OAuth client id issued by Dialpad."]);
                }

                if (string.IsNullOrEmpty(settings.ClientSecret) && string.IsNullOrWhiteSpace(model.ClientSecret))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ClientSecret), S["Enter the OAuth client secret issued by Dialpad."]);
                }
            }
            else if (model.AuthenticationType == DialpadAuthenticationType.ApiKey)
            {
                if (string.IsNullOrEmpty(settings.ApiToken) && string.IsNullOrWhiteSpace(model.ApiToken))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiToken), S["Enter the Dialpad API key."]);
                }

                if (string.IsNullOrWhiteSpace(model.UserId))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserId), S["Enter the Dialpad user id that places outbound calls."]);
                }
            }

            if (!string.IsNullOrWhiteSpace(model.ApiToken))
            {
                var protector = _dataProtectionProvider.CreateProtector(DialpadConstants.ProtectorName);
                var protectedToken = protector.Protect(model.ApiToken);

                hasChanges |= settings.ApiToken != protectedToken;

                settings.ApiToken = protectedToken;
            }

            if (!string.IsNullOrWhiteSpace(model.ClientSecret))
            {
                var protector = _dataProtectionProvider.CreateProtector(DialpadConstants.OAuthProtectorName);
                var protectedSecret = protector.Protect(model.ClientSecret);

                hasChanges |= settings.ClientSecret != protectedSecret;

                settings.ClientSecret = protectedSecret;
            }

            if (!string.IsNullOrWhiteSpace(model.WebhookSigningSecret))
            {
                var protector = _dataProtectionProvider.CreateProtector(DialpadConstants.WebhookProtectorName);
                var protectedWebhookSecret = protector.Protect(model.WebhookSigningSecret);

                hasChanges |= settings.WebhookSigningSecret != protectedWebhookSecret;

                settings.WebhookSigningSecret = protectedWebhookSecret;
            }
        }

        if (context.Updater.ModelState.IsValid && settings.IsEnabled && string.IsNullOrEmpty(telephonySettings.DefaultProviderName))
        {
            telephonySettings.DefaultProviderName = DialpadConstants.ProviderTechnicalName;

            site.Put(telephonySettings);

            hasChanges = true;
        }

        if (hasChanges)
        {
            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }

    private static DialpadAuthenticationType GetEffectiveAuthenticationType(DialpadSettings settings)
    {
        if (settings.AuthenticationType != DialpadAuthenticationType.NotConfigured)
        {
            return settings.AuthenticationType;
        }

        if (!string.IsNullOrEmpty(settings.ApiToken))
        {
            return DialpadAuthenticationType.ApiKey;
        }

        if (!string.IsNullOrWhiteSpace(settings.ClientId) || !string.IsNullOrEmpty(settings.ClientSecret))
        {
            return DialpadAuthenticationType.OAuth2;
        }

        return DialpadAuthenticationType.NotConfigured;
    }
}
