using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Receipts.Core;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Drivers;
using CrestApps.OrchardCore.Transactions.Migrations;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Navigation;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.Tasks;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// Registers the provider-agnostic transaction ledger, its report and management screens, and permissions.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The tenant configuration the customer pages options are read from.</param>
    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

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
            .AddNavigationProvider<TransactionsAccountMenu>()
            .AddPermissionProvider<TransactionsPermissionProvider>();

        services.AddSignedLinks();
        services.AddScoped<ITransactionPayLinkService, TransactionPayLinkService>();
        services.AddScoped<TransactionDisplayText>();
        services.AddScoped<ITransactionCommandService, DefaultTransactionCommandService>();

        // The rows of the transactions report and of the customer's own statement.
        services
            .AddDisplayDriver<Transaction, TransactionDisplayDriver>()
            .AddDisplayDriver<CustomerTransaction, CustomerTransactionDisplayDriver>();
        services.AddScoped<IPaymentRefundHandler, TransactionRefundHandler>();

        // The customer pages in the admin and in the site theme, and the links to them sent in messages.
        services.Configure<TransactionCustomerPagesOptions>(_shellConfiguration.GetSection(TransactionsConstants.CustomerPagesConfigurationSection));
        services.AddScoped<ITransactionCustomerPageService, TransactionCustomerPageService>();
        services.AddScoped<ITransactionCustomerLinkService, TransactionCustomerLinkService>();
        services.AddScoped<ICommercePersonalDataContributor, TransactionPersonalDataContributor>();
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
        services.AddNavigationProvider<PaymentLedgerAdminMenu>();

        // The rows of the payments and refunds ledgers.
        services
            .AddDisplayDriver<PaymentAttempt, PaymentAttemptDisplayDriver>()
            .AddDisplayDriver<PaymentRefund, PaymentRefundDisplayDriver>();
        services.AddScoped<ICommercePersonalDataContributor, TransactionSettlementPersonalDataContributor>();
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

        // Lets a role that holds only the transaction settings permission open Settings > Commerce > Transactions.
        services.AddSiteSettingsPermission(TransactionsConstants.SettingsGroupId, TransactionsPermissions.ManageTransactionSettings);

        services.AddSingleton<IBackgroundTask, TransactionReminderBackgroundTask>();
    }
}

/// <summary>
/// Registers the printable receipt of every payment and the printable invoice of a transaction, whenever the
/// Receipts module is enabled too. They only show what the ledger already holds, so they need no opt-in.
/// </summary>
[RequireFeatures(ReceiptsConstants.Feature.Area)]
public sealed class ReceiptPagesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ITransactionReceiptBuilder, TransactionReceiptBuilder>();
        services.AddScoped<ITransactionInvoiceBuilder, TransactionInvoiceBuilder>();
    }
}

/// <summary>
/// Sends the customer a numbered receipt after every payment applied to a transaction, and a notice after every
/// refund. It messages customers, so it is a feature of its own that a site turns on deliberately.
/// </summary>
[Feature(TransactionsConstants.Features.Receipts)]
public sealed class ReceiptsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<PaymentReceiptTransactionPaymentHandler>();
        services.AddScoped<ITransactionPaymentHandler>(sp => sp.GetRequiredService<PaymentReceiptTransactionPaymentHandler>());
        services.AddScoped<IPaymentRefundHandler>(sp => sp.GetRequiredService<PaymentReceiptTransactionPaymentHandler>());
    }
}
