using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Ensure all payment providers are configured before setting the default using post-configurations.
/// </summary>
public sealed class DefaultPaymentMethodConfigurations : IPostConfigureOptions<PaymentMethodOptions>
{
    private readonly ISiteService _siteService;

    public DefaultPaymentMethodConfigurations(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void PostConfigure(string name, PaymentMethodOptions options)
    {
        var settings = _siteService.GetSettingsAsync<SubscriptionSettings>()
            .GetAwaiter()
            .GetResult();

        if (!string.IsNullOrEmpty(settings.DefaultPaymentMethod) &&
            options.PaymentMethods.Any(x => string.Equals(x.Key, settings.DefaultPaymentMethod, StringComparison.Ordinal)))
        {
            options.DefaultPaymentMethod = settings.DefaultPaymentMethod;
        }
        else
        {
            options.DefaultPaymentMethod = options.PaymentMethods
                .OrderBy(x => x.HasProcessor ? 0 : 1)
                .ThenBy(options.PaymentMethods.IndexOf)
                .FirstOrDefault()?.Key;
        }
    }
}
