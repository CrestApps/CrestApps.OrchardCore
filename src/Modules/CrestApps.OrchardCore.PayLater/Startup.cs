using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.PayLater.Drivers;
using CrestApps.OrchardCore.PayLater.Handlers;
using CrestApps.OrchardCore.PayLater.Navigation;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
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
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICheckoutPaymentProvider, PayLaterCheckoutPaymentProvider>();
        services.AddScoped<ICheckoutHandler, PayLaterTransactionCheckoutHandler>();

        services.AddTransactionSource(PayLaterCheckoutPaymentProvider.ProcessorKey, source =>
        {
            source.DisplayName = S["Pay Later"];
            source.Description = S["Outstanding balances committed through the offline Pay Later option."];
        });

        services
            .AddSiteDisplayDriver<PayLaterSettingsDisplayDriver>()
            .AddNavigationProvider<PayLaterAdminMenu>();
    }
}
