using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Drivers;
using CrestApps.OrchardCore.Transactions.Migrations;
using CrestApps.OrchardCore.Transactions.Navigation;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.Tasks;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// Registers the provider-agnostic transaction ledger, its report and management screens, and permissions.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(TransactionsConstants.CollectionName));

        services
            .AddTransactionsCore()
            .AddCustomersCore()
            .AddDataMigration<TransactionMigrations>()
            .AddIndexProvider<TransactionIndexProvider>();

        services
            .AddNavigationProvider<TransactionsAdminMenu>()
            .AddPermissionProvider<TransactionsPermissionProvider>();
    }
}

/// <summary>
/// Wires the online settlement path so a checkout that references a transaction settles it automatically.
/// </summary>
[RequireFeatures(CheckoutConstants.Features.Area)]
public sealed class CheckoutStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICheckoutHandler, TransactionSettlementCheckoutHandler>();
    }
}

/// <summary>
/// Registers the opt-in reminder pipeline that delivers outstanding-payment reminders through the
/// notification system so each reminder honors the owner's channel preference rather than assuming email.
/// </summary>
[Feature(TransactionsConstants.Features.Notification)]
public sealed class NotificationStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ITransactionReminderService, DefaultTransactionReminderService>();

        services
            .AddSiteDisplayDriver<TransactionReminderSettingsDisplayDriver>()
            .AddNavigationProvider<TransactionReminderSettingsAdminMenu>();

        services.AddSingleton<IBackgroundTask, TransactionReminderBackgroundTask>();
    }
}
