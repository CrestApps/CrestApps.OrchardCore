using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Drivers;
using CrestApps.OrchardCore.Stripe.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Builds the runtime <see cref="StripeOptions"/> from the application configuration and the stored site settings.
/// </summary>
/// <remarks>
/// <para>
/// The application configuration (the <c>CrestApps_Stripe</c> section) wins over the settings page, one value at a
/// time: <see cref="StripeConfigurationOptions.IsLive"/> when it is set, and each key of the active mode when it has a
/// value. Every value the configuration leaves out falls back to the stored setting.
/// </para>
/// <para>
/// Configuration wins because it is owned by whoever deploys the tenant. Keys kept in environment variables or a
/// secret store are the ones an operator rotates and audits, so an administrator editing the page must not be able to
/// shadow them silently; the page shows those values as configured from the application settings instead.
/// </para>
/// </remarks>
internal sealed class StripeOptionsConfiguration : IConfigureOptions<StripeOptions>
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly StripeConfigurationOptions _configurationOptions;

    public StripeOptionsConfiguration(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<StripeConfigurationOptions> configurationOptions)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _configurationOptions = configurationOptions.Value;
    }

    public void Configure(StripeOptions options)
    {
        var settings = _siteService.GetSettings<StripeSettings>();

        var isLive = _configurationOptions.IsLive ?? settings.IsLive;
        var configured = _configurationOptions.GetKeys(isLive);

        var storedPublishableKey = isLive ? settings.LivePublishableKey : settings.TestPublishableKey;
        var storedSecretKey = isLive ? settings.LivePrivateSecret : settings.TestPrivateSecret;
        var storedWebhookSecret = isLive ? settings.LiveWebhookSecret : settings.TestWebhookSecret;

        options.IsLive = isLive;

        options.PublishableKey = configured.HasPublishableKey
            ? configured.PublishableKey.Trim()
            : storedPublishableKey;

        // A stored secret is only unprotected when the configuration does not supply one, so a tenant whose secrets
        // live in configuration never depends on the database holding a readable value.
        options.ApiKey = configured.HasSecretKey
            ? configured.SecretKey.Trim()
            : _dataProtectionProvider.UnprotectSetting(StripeSettingsDisplayDriver.ProtectionPurpose, storedSecretKey);

        options.WebhookSecret = configured.HasWebhookSecret
            ? configured.WebhookSecret.Trim()
            : _dataProtectionProvider.UnprotectSetting(StripeSettingsDisplayDriver.ProtectionPurpose, storedWebhookSecret);
    }
}
