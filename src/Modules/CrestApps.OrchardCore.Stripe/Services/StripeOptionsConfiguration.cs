using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Drivers;
using CrestApps.OrchardCore.Stripe.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Stripe.Services;

internal sealed class StripeOptionsConfiguration : IConfigureOptions<StripeOptions>
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public StripeOptionsConfiguration(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
    }

    public void Configure(StripeOptions options)
    {
        var settings = _siteService.GetSettingsAsync<StripeSettings>()
            .GetAwaiter()
            .GetResult();

        var protector = _dataProtectionProvider.CreateProtector(StripeSettingsDisplayDriver.ProtectionPurpose);

        options.IsLive = settings.IsLive;

        if (settings.IsLive)
        {
            options.PublishableKey = settings.LivePublishableKey;

            if (!string.IsNullOrEmpty(settings.LivePrivateSecret))
            {
                options.ApiKey = protector.Unprotect(settings.LivePrivateSecret);
            }

            if (!string.IsNullOrEmpty(settings.LiveWebhookSecret))
            {
                options.WebhookSecret = protector.Unprotect(settings.LiveWebhookSecret);
            }

            return;
        }

        options.PublishableKey = settings.TestPublishableKey;

        if (!string.IsNullOrEmpty(settings.TestPrivateSecret))
        {
            options.ApiKey = protector.Unprotect(settings.TestPrivateSecret);
        }

        if (!string.IsNullOrEmpty(settings.TestWebhookSecret))
        {
            options.WebhookSecret = protector.Unprotect(settings.TestWebhookSecret);
        }
    }
}
