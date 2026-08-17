using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.PayLater.Drivers;
using CrestApps.OrchardCore.PayLater.Handlers;
using CrestApps.OrchardCore.PayLater.Navigation;
using CrestApps.OrchardCore.PayLater.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater;

/// <summary>
/// Registers the offline Pay Later payment provider with the checkout framework, records its commitments
/// as outstanding transactions, and exposes the Pay Later settings.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICheckoutPaymentProvider, PayLaterCheckoutPaymentProvider>();
        services.AddScoped<ICheckoutHandler, PayLaterTransactionCheckoutHandler>();

        services
            .AddSiteDisplayDriver<PayLaterSettingsDisplayDriver>()
            .AddNavigationProvider<PayLaterAdminMenu>();
    }
}
