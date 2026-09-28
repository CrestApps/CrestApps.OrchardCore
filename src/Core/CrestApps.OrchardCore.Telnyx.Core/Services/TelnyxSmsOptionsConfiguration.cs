using CrestApps.OrchardCore.Telnyx.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Resolves <see cref="TelnyxSmsOptions"/> by merging the configuration-driven defaults (the
/// <c>OrchardCore_Sms_Telnyx</c> appsettings section) with the UI-driven site settings, which override when the
/// tenant has enabled the Telnyx SMS provider. Secrets stored in the UI settings are unprotected here so the
/// provider and webhook read plaintext options.
/// </summary>
internal sealed class TelnyxSmsOptionsConfiguration : IConfigureOptions<TelnyxSmsOptions>
{
    private readonly IShellConfiguration _shellConfiguration;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    public TelnyxSmsOptionsConfiguration(
        IShellConfiguration shellConfiguration,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<TelnyxSmsOptionsConfiguration> logger)
    {
        _shellConfiguration = shellConfiguration;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Configure(TelnyxSmsOptions options)
    {
        // 1. Configuration-driven defaults (appsettings). This is the "default" provider: it is enabled purely
        //    from configuration, with no UI, when the section supplies an API key.
        var section = _shellConfiguration.GetSection(TelnyxConstants.SmsConfigurationSection);

        options.ApiKey = section["ApiKey"];
        options.MessagingProfileId = section["MessagingProfileId"];
        options.WebhookPublicKey = section["WebhookPublicKey"];
        options.ApiBaseUrl = ResolveApiBaseUrl(section["ApiBaseUrl"]);
        options.IsEnabled = !string.IsNullOrWhiteSpace(options.ApiKey);

        // 2. UI-driven site settings override the defaults when the tenant enabled the provider.
        var settings = _siteService.GetSettings<TelnyxSmsSettings>();

        if (settings.IsEnabled)
        {
            options.IsEnabled = true;

            var apiKeyProtector = _dataProtectionProvider.CreateProtector(TelnyxConstants.SmsApiKeyProtectorName);
            var webhookProtector = _dataProtectionProvider.CreateProtector(TelnyxConstants.SmsWebhookProtectorName);

            if (!string.IsNullOrEmpty(settings.ApiKey))
            {
                options.ApiKey = Unprotect(apiKeyProtector, settings.ApiKey) ?? options.ApiKey;
            }

            if (!string.IsNullOrEmpty(settings.MessagingProfileId))
            {
                options.MessagingProfileId = settings.MessagingProfileId.Trim();
            }

            if (!string.IsNullOrEmpty(settings.WebhookPublicKey))
            {
                options.WebhookPublicKey = Unprotect(webhookProtector, settings.WebhookPublicKey) ?? options.WebhookPublicKey;
            }

            if (!string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
            {
                options.ApiBaseUrl = ResolveApiBaseUrl(settings.ApiBaseUrl);
            }
        }

        // The provider cannot function without an API key, regardless of the enable flag.
        options.IsEnabled = options.IsEnabled && !string.IsNullOrWhiteSpace(options.ApiKey);
    }

    private static string ResolveApiBaseUrl(string configuredBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return TelnyxConstants.DefaultApiBaseUrl;
        }

        var trimmed = configuredBaseUrl.Trim();

        return trimmed.EndsWith('/') ? trimmed : trimmed + '/';
    }

    private string Unprotect(IDataProtector protector, string value)
    {
        try
        {
            return protector.Unprotect(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to unprotect a Telnyx SMS secret.");

            return null;
        }
    }
}
