using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
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
/// Registers the provider-agnostic transaction ledger, its report and settings screens, permissions, and
/// the scheduled reminder sweep.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddTransactionsCore()
            .AddDataMigration<TransactionMigrations>()
            .AddIndexProvider<TransactionIndexProvider>();

        services.AddScoped<ITransactionReminderService, DefaultTransactionReminderService>();

        services
            .AddSiteDisplayDriver<TransactionReminderSettingsDisplayDriver>()
            .AddNavigationProvider<TransactionsAdminMenu>()
            .AddPermissionProvider<TransactionsPermissionProvider>();

        services.AddSingleton<IBackgroundTask, TransactionReminderBackgroundTask>();
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
