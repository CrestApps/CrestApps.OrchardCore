using System.Security.Cryptography;
using CrestApps.OrchardCore.Dialpad.Models;
using CrestApps.OrchardCore.Dialpad.ViewModels;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger _logger;

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
    /// <param name="logger">The logger.</param>
    public DialpadSettingsDisplayDriver(
        IShellReleaseManager shellReleaseManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        INotifier notifier,
        IHtmlLocalizer<DialpadSettingsDisplayDriver> htmlLocalizer,
        IStringLocalizer<DialpadSettingsDisplayDriver> stringLocalizer,
        ILogger<DialpadSettingsDisplayDriver> logger)
    {
        _shellReleaseManager = shellReleaseManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _notifier = notifier;
        _logger = logger;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, DialpadSettings settings, BuildEditorContext context)
    {
        Task<bool> CanManageAsync()
            => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TelephonyPermissions.ManageTelephonySettings);

        return Combine(
            Initialize<DialpadSettingsViewModel>("DialpadSettings_Edit", model =>
            {
                model.IsEnabled = settings.IsEnabled;
                model.Environment = settings.Environment;
            }).Location("Content:10#Dialpad;1")
            .RenderWhen(CanManageAsync)
            .OnGroup(SettingsGroupId),

            Initialize<DialpadEnvironmentSettingsViewModel>("DialpadEnvironmentSettings_Edit", model =>
            {
                MapEnvironmentToViewModel(settings.GetEnvironmentSettings(DialpadEnvironment.Production), model);
            }).Location("Content:10#Dialpad%Production;5")
            .Differentiator(nameof(DialpadSettingsViewModel.Production))
            .Prefix($"{Prefix}.{nameof(DialpadSettingsViewModel.Production)}")
            .RenderWhen(CanManageAsync)
            .OnGroup(SettingsGroupId),

            Initialize<DialpadEnvironmentSettingsViewModel>("DialpadEnvironmentSettings_Edit", model =>
            {
                MapEnvironmentToViewModel(settings.GetEnvironmentSettings(DialpadEnvironment.Sandbox), model);
            }).Location("Content:10#Dialpad%Sandbox;10")
            .Differentiator(nameof(DialpadSettingsViewModel.Sandbox))
            .Prefix($"{Prefix}.{nameof(DialpadSettingsViewModel.Sandbox)}")
            .RenderWhen(CanManageAsync)
            .OnGroup(SettingsGroupId)
        );
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
            settings.Environment = model.Environment;

            hasChanges |= UpdateEnvironment(DialpadEnvironment.Production, settings.GetEnvironmentSettings(DialpadEnvironment.Production), model.Production, model.Environment == DialpadEnvironment.Production, context);
            hasChanges |= UpdateEnvironment(DialpadEnvironment.Sandbox, settings.GetEnvironmentSettings(DialpadEnvironment.Sandbox), model.Sandbox, model.Environment == DialpadEnvironment.Sandbox, context);
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

    private void MapEnvironmentToViewModel(DialpadEnvironmentSettings environment, DialpadEnvironmentSettingsViewModel model)
    {
        model.AuthenticationType = environment.GetEffectiveAuthenticationType();
        model.Host = environment.Host;
        model.ClientId = environment.ClientId;
        model.Scopes = environment.Scopes;
        model.UserId = environment.UserId;
        model.OutboundCallerId = environment.OutboundCallerId;
        model.HasApiToken = !string.IsNullOrEmpty(environment.ApiToken);
        model.HasClientSecret = !string.IsNullOrEmpty(environment.ClientSecret);
        model.HasUnreadableClientSecret = !string.IsNullOrEmpty(environment.ClientSecret) &&
            !CanUnprotect(environment.ClientSecret, DialpadConstants.OAuthProtectorName);
        model.HasWebhookSigningSecret = !string.IsNullOrEmpty(environment.WebhookSigningSecret);
    }

    private bool UpdateEnvironment(
        DialpadEnvironment environmentType,
        DialpadEnvironmentSettings environment,
        DialpadEnvironmentSettingsViewModel model,
        bool isActive,
        UpdateEditorContext context)
    {
        var prefix = environmentType == DialpadEnvironment.Sandbox ? nameof(DialpadSettingsViewModel.Sandbox) : nameof(DialpadSettingsViewModel.Production);
        var hasChanges = false;

        var host = string.IsNullOrWhiteSpace(model.Host) ? null : model.Host.Trim();

        hasChanges |= environment.AuthenticationType != model.AuthenticationType;
        hasChanges |= environment.Host != host;
        hasChanges |= environment.UserId != model.UserId;
        hasChanges |= environment.OutboundCallerId != model.OutboundCallerId;
        hasChanges |= environment.ClientId != model.ClientId;
        hasChanges |= environment.Scopes != model.Scopes;
        environment.AuthenticationType = model.AuthenticationType;
        environment.Host = host;
        environment.UserId = model.UserId;
        environment.OutboundCallerId = model.OutboundCallerId;
        environment.ClientId = model.ClientId;
        environment.Scopes = model.Scopes;

        if (!string.IsNullOrWhiteSpace(model.ApiToken))
        {
            var protector = _dataProtectionProvider.CreateProtector(DialpadConstants.ProtectorName);
            var protectedToken = protector.Protect(model.ApiToken);

            hasChanges |= environment.ApiToken != protectedToken;

            environment.ApiToken = protectedToken;
        }

        if (!string.IsNullOrWhiteSpace(model.ClientSecret))
        {
            var protector = _dataProtectionProvider.CreateProtector(DialpadConstants.OAuthProtectorName);
            var protectedSecret = protector.Protect(model.ClientSecret);

            hasChanges |= environment.ClientSecret != protectedSecret;

            environment.ClientSecret = protectedSecret;
        }

        if (!string.IsNullOrWhiteSpace(model.WebhookSigningSecret))
        {
            var protector = _dataProtectionProvider.CreateProtector(DialpadConstants.WebhookProtectorName);
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
        DialpadEnvironmentSettings environment,
        DialpadEnvironmentSettingsViewModel model,
        string prefix,
        UpdateEditorContext context)
    {
        if (!Enum.IsDefined(model.AuthenticationType) || model.AuthenticationType == DialpadAuthenticationType.NotConfigured)
        {
            context.Updater.ModelState.AddModelError(Prefix, $"{prefix}.{nameof(model.AuthenticationType)}", S["Select a Dialpad authentication type for the active environment."]);

            return;
        }

        if (model.AuthenticationType == DialpadAuthenticationType.OAuth2)
        {
            if (string.IsNullOrWhiteSpace(model.ClientId))
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{prefix}.{nameof(model.ClientId)}", S["Enter the OAuth client id issued by Dialpad."]);
            }

            if ((string.IsNullOrEmpty(environment.ClientSecret) ||
                !CanUnprotect(environment.ClientSecret, DialpadConstants.OAuthProtectorName)) &&
                string.IsNullOrWhiteSpace(model.ClientSecret))
            {
                context.Updater.ModelState.AddModelError(
                    Prefix,
                    $"{prefix}.{nameof(model.ClientSecret)}",
                    S["The saved OAuth client secret cannot be decrypted with the current data-protection keys. Re-enter the client secret."]);
            }
        }
        else if (model.AuthenticationType == DialpadAuthenticationType.ApiKey)
        {
            if (string.IsNullOrEmpty(environment.ApiToken) && string.IsNullOrWhiteSpace(model.ApiToken))
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{prefix}.{nameof(model.ApiToken)}", S["Enter the Dialpad API key."]);
            }

            if (string.IsNullOrWhiteSpace(model.UserId))
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{prefix}.{nameof(model.UserId)}", S["Enter the Dialpad user id that places outbound calls."]);
            }
        }
    }

    private bool CanUnprotect(string value, string protectorName)
    {
        try
        {
            return !string.IsNullOrEmpty(_dataProtectionProvider.CreateProtector(protectorName).Unprotect(value));
        }
        catch (CryptographicException exception)
        {
            _logger.LogWarning(exception, "A saved Dialpad secret cannot be decrypted with the current data-protection keys.");

            return false;
        }
    }
}
