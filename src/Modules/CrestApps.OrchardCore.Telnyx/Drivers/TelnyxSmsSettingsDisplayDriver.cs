using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telnyx.Drivers;

/// <summary>
/// Renders and persists the UI-driven Telnyx SMS provider settings on the SMS settings group
/// (Configuration → Settings → SMS), alongside OrchardCore's general SMS settings and the Twilio provider.
/// Secrets are stored protected and never re-rendered.
/// </summary>
public sealed class TelnyxSmsSettingsDisplayDriver : SiteDisplayDriver<TelnyxSmsSettings>
{
    // Matches OrchardCore's SMS settings group so the Telnyx provider appears under /Admin/Settings/sms.
    private const string SmsSettingsGroupId = "sms";

    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IShellReleaseManager _shellReleaseManager;

    internal readonly IStringLocalizer S;

    public TelnyxSmsSettingsDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IShellReleaseManager shellReleaseManager,
        IStringLocalizer<TelnyxSmsSettingsDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _shellReleaseManager = shellReleaseManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId => SmsSettingsGroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, TelnyxSmsSettings settings, BuildEditorContext context)
    {
        return Initialize<TelnyxSmsSettingsViewModel>("TelnyxSmsSettings_Edit", model =>
            {
                model.IsEnabled = settings.IsEnabled;
                model.HasApiKey = !string.IsNullOrEmpty(settings.ApiKey);
                model.MessagingProfileId = settings.MessagingProfileId;
                model.HasWebhookPublicKey = !string.IsNullOrEmpty(settings.WebhookPublicKey);
                model.ApiBaseUrl = settings.ApiBaseUrl;
            })
            .Location("Content:5#Telnyx;20")
            .OnGroup(SettingsGroupId);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, TelnyxSmsSettings settings, UpdateEditorContext context)
    {
        var model = new TelnyxSmsSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.IsEnabled = model.IsEnabled;
        settings.MessagingProfileId = model.MessagingProfileId?.Trim();
        settings.ApiBaseUrl = model.ApiBaseUrl?.Trim();

        var apiKeyProtector = _dataProtectionProvider.CreateProtector(TelnyxConstants.SmsApiKeyProtectorName);
        var webhookProtector = _dataProtectionProvider.CreateProtector(TelnyxConstants.SmsWebhookProtectorName);

        // Only overwrite a stored secret when the operator entered a new value.
        if (!string.IsNullOrWhiteSpace(model.ApiKey))
        {
            settings.ApiKey = apiKeyProtector.Protect(model.ApiKey.Trim());
        }

        if (!string.IsNullOrWhiteSpace(model.WebhookPublicKey))
        {
            settings.WebhookPublicKey = webhookProtector.Protect(model.WebhookPublicKey.Trim());
        }

        // Validate: an enabled provider needs an API key (stored or newly entered).
        if (settings.IsEnabled && string.IsNullOrEmpty(settings.ApiKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["An API key is required to enable the Telnyx SMS provider."]);
        }

        if (context.Updater.ModelState.IsValid)
        {
            // The provider options and the provider list are resolved from these settings; request a shell
            // release so the new configuration takes effect.
            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }
}
