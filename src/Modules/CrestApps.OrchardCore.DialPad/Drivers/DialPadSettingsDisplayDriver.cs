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
        Task<bool> CanManageAsync()
            => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TelephonyPermissions.ManageTelephonySettings);

        return Combine(
            Initialize<DialPadSettingsViewModel>("DialPadSettings_Edit", model =>
            {
                model.IsEnabled = settings.IsEnabled;
                model.Environment = settings.Environment;
            }).Location("Content:10#DialPad;1")
            .RenderWhen(CanManageAsync)
            .OnGroup(SettingsGroupId),

            Initialize<DialPadEnvironmentSettingsViewModel>("DialPadEnvironmentSettings_Edit", model =>
            {
                MapEnvironmentToViewModel(settings.GetEnvironmentSettings(DialPadEnvironment.Production), model);
            }).Location("Content:10#DialPad%Production;5")
            .Differentiator(nameof(DialPadSettingsViewModel.Production))
            .Prefix($"{Prefix}.{nameof(DialPadSettingsViewModel.Production)}")
            .RenderWhen(CanManageAsync)
            .OnGroup(SettingsGroupId),

            Initialize<DialPadEnvironmentSettingsViewModel>("DialPadEnvironmentSettings_Edit", model =>
            {
                MapEnvironmentToViewModel(settings.GetEnvironmentSettings(DialPadEnvironment.Sandbox), model);
            }).Location("Content:10#DialPad%Sandbox;10")
            .Differentiator(nameof(DialPadSettingsViewModel.Sandbox))
            .Prefix($"{Prefix}.{nameof(DialPadSettingsViewModel.Sandbox)}")
            .RenderWhen(CanManageAsync)
            .OnGroup(SettingsGroupId)
        );
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

            hasChanges |= settings.Environment != model.Environment;
            settings.Environment = model.Environment;

            hasChanges |= UpdateEnvironment(DialPadEnvironment.Production, settings.GetEnvironmentSettings(DialPadEnvironment.Production), model.Production, model.Environment == DialPadEnvironment.Production, context);
            hasChanges |= UpdateEnvironment(DialPadEnvironment.Sandbox, settings.GetEnvironmentSettings(DialPadEnvironment.Sandbox), model.Sandbox, model.Environment == DialPadEnvironment.Sandbox, context);
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

    private static void MapEnvironmentToViewModel(DialPadEnvironmentSettings environment, DialPadEnvironmentSettingsViewModel model)
    {
        model.AuthenticationType = environment.GetEffectiveAuthenticationType();
        model.ClientId = environment.ClientId;
        model.Scopes = environment.Scopes;
        model.UserId = environment.UserId;
        model.OutboundCallerId = environment.OutboundCallerId;
        model.HasApiToken = !string.IsNullOrEmpty(environment.ApiToken);
        model.HasClientSecret = !string.IsNullOrEmpty(environment.ClientSecret);
        model.HasWebhookSigningSecret = !string.IsNullOrEmpty(environment.WebhookSigningSecret);
    }

    private bool UpdateEnvironment(
        DialPadEnvironment environmentType,
        DialPadEnvironmentSettings environment,
        DialPadEnvironmentSettingsViewModel model,
        bool isActive,
        UpdateEditorContext context)
    {
        var prefix = environmentType == DialPadEnvironment.Sandbox ? nameof(DialPadSettingsViewModel.Sandbox) : nameof(DialPadSettingsViewModel.Production);
        var hasChanges = false;

        hasChanges |= environment.AuthenticationType != model.AuthenticationType;
        hasChanges |= environment.UserId != model.UserId;
        hasChanges |= environment.OutboundCallerId != model.OutboundCallerId;
        hasChanges |= environment.ClientId != model.ClientId;
        hasChanges |= environment.Scopes != model.Scopes;
        environment.AuthenticationType = model.AuthenticationType;
        environment.UserId = model.UserId;
        environment.OutboundCallerId = model.OutboundCallerId;
        environment.ClientId = model.ClientId;
        environment.Scopes = model.Scopes;

        if (!string.IsNullOrWhiteSpace(model.ApiToken))
        {
            var protector = _dataProtectionProvider.CreateProtector(DialPadConstants.ProtectorName);
            var protectedToken = protector.Protect(model.ApiToken);

            hasChanges |= environment.ApiToken != protectedToken;

            environment.ApiToken = protectedToken;
        }

        if (!string.IsNullOrWhiteSpace(model.ClientSecret))
        {
            var protector = _dataProtectionProvider.CreateProtector(DialPadConstants.OAuthProtectorName);
            var protectedSecret = protector.Protect(model.ClientSecret);

            hasChanges |= environment.ClientSecret != protectedSecret;

            environment.ClientSecret = protectedSecret;
        }

        if (!string.IsNullOrWhiteSpace(model.WebhookSigningSecret))
        {
            var protector = _dataProtectionProvider.CreateProtector(DialPadConstants.WebhookProtectorName);
            var protectedWebhookSecret = protector.Protect(model.WebhookSigningSecret);

            hasChanges |= environment.WebhookSigningSecret != protectedWebhookSecret;

            environment.WebhookSigningSecret = protectedWebhookSecret;
        }

        if (isActive)
        {
            ValidateActiveEnvironment(environment, model, prefix, context);
        }

        return hasChanges;
    }

    private void ValidateActiveEnvironment(
        DialPadEnvironmentSettings environment,
        DialPadEnvironmentSettingsViewModel model,
        string prefix,
        UpdateEditorContext context)
    {
        if (!Enum.IsDefined(model.AuthenticationType) || model.AuthenticationType == DialPadAuthenticationType.NotConfigured)
        {
            context.Updater.ModelState.AddModelError(Prefix, $"{prefix}.{nameof(model.AuthenticationType)}", S["Select a DialPad authentication type for the active environment."]);

            return;
        }

        if (model.AuthenticationType == DialPadAuthenticationType.OAuth2)
        {
            if (string.IsNullOrWhiteSpace(model.ClientId))
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{prefix}.{nameof(model.ClientId)}", S["Enter the OAuth client id issued by DialPad."]);
            }

            if (string.IsNullOrEmpty(environment.ClientSecret) && string.IsNullOrWhiteSpace(model.ClientSecret))
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{prefix}.{nameof(model.ClientSecret)}", S["Enter the OAuth client secret issued by DialPad."]);
            }
        }
        else if (model.AuthenticationType == DialPadAuthenticationType.ApiKey)
        {
            if (string.IsNullOrEmpty(environment.ApiToken) && string.IsNullOrWhiteSpace(model.ApiToken))
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{prefix}.{nameof(model.ApiToken)}", S["Enter the DialPad API key."]);
            }

            if (string.IsNullOrWhiteSpace(model.UserId))
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{prefix}.{nameof(model.UserId)}", S["Enter the DialPad user id that places outbound calls."]);
            }
        }
    }
}
