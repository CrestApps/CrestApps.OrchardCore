using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.PayLater.Drivers;
using CrestApps.OrchardCore.PayLater.Handlers;
using CrestApps.OrchardCore.PayLater.Navigation;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
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
        services.AddScoped<PayLaterCheckoutPaymentProvider>();
        services.AddScoped<ICheckoutPaymentProvider>(sp => sp.GetRequiredService<PayLaterCheckoutPaymentProvider>());
        services.AddScoped<ICheckoutRecurringPaymentProvider>(sp => sp.GetRequiredService<PayLaterCheckoutPaymentProvider>());
        services.AddScoped<OfflineCheckoutPaymentProvider>(sp => sp.GetRequiredService<PayLaterCheckoutPaymentProvider>());

        // Bank transfer and Cash on delivery are offline methods too, each off until the store turns it on.
        services.AddScoped<BankTransferCheckoutPaymentProvider>();
        services.AddScoped<IStoreSetupStep, OfflinePaymentsSetupStep>();
        services.AddScoped<ICheckoutPaymentProvider>(sp => sp.GetRequiredService<BankTransferCheckoutPaymentProvider>());
        services.AddScoped<OfflineCheckoutPaymentProvider>(sp => sp.GetRequiredService<BankTransferCheckoutPaymentProvider>());
        services.AddScoped<CashOnDeliveryCheckoutPaymentProvider>();
        services.AddScoped<ICheckoutPaymentProvider>(sp => sp.GetRequiredService<CashOnDeliveryCheckoutPaymentProvider>());
        services.AddScoped<OfflineCheckoutPaymentProvider>(sp => sp.GetRequiredService<CashOnDeliveryCheckoutPaymentProvider>());
        services.AddScoped<OfflinePaymentLookup>();
        services.AddDisplayDriver<CheckoutFlowPaymentMethod, OfflinePaymentMethodDisplayDriver>();
        services.AddDisplayDriver<CheckoutFlow, OfflinePaymentConfirmationDisplayDriver>();
        services.AddSingleton<IBackgroundTask, PayLaterRenewalBackgroundTask>();
        services.AddScoped<ICheckoutHandler, PayLaterTransactionCheckoutHandler>();
        services.AddDisplayDriver<CheckoutFlowPaymentMethod, PayLaterCheckoutPaymentMethodDisplayDriver>();

        services.AddTransactionSource(PayLaterCheckoutPaymentProvider.ProcessorKey, source =>
        {
            source.DisplayName = S["Pay Later"];
            source.Description = S["Outstanding balances committed through the offline Pay Later option."];
        });

        services.AddTransactionSource(BankTransferCheckoutPaymentProvider.ProcessorKey, source =>
        {
            source.DisplayName = S["Bank transfer"];
            source.Description = S["Orders the buyer pays by transferring the money from their bank."];
        });

        services.AddTransactionSource(CashOnDeliveryCheckoutPaymentProvider.ProcessorKey, source =>
        {
            source.DisplayName = S["Cash on delivery"];
            source.Description = S["Orders the buyer pays when they are delivered."];
        });

        services
            .AddSiteDisplayDriver<PayLaterSettingsDisplayDriver>()
            .AddSiteDisplayDriver<BankTransferSettingsDisplayDriver>()
            .AddSiteDisplayDriver<CashOnDeliverySettingsDisplayDriver>()
            .AddNavigationProvider<PayLaterAdminMenu>();

        // The Pay Later settings group is guarded by the transaction settings permission, so a role that holds
        // only that permission must be allowed to open the group's settings page.
        services.AddSiteSettingsPermission(PayLaterConstants.SettingsGroupId, TransactionsPermissions.ManageTransactionSettings);
    }
}

/// <summary>
/// How to pay an order placed with an offline method, on its pages, when Orders is enabled.
/// </summary>
[RequireFeatures(OrderConstants.Features.Orders)]
public sealed class OrdersStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDisplayDriver<Order, OrderOfflinePaymentDisplayDriver>();
}
