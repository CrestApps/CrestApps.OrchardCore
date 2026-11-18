using CrestApps.Core.Services;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Drivers;
using CrestApps.OrchardCore.Transactions.Handlers;
using CrestApps.OrchardCore.Transactions.Migrations;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Navigation;
using CrestApps.OrchardCore.Transactions.Recipes;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// Registers the payment terms catalog, its admin pages and its recipe step.
/// </summary>
[Feature(TransactionsConstants.Features.PaymentTerms)]
public sealed class PaymentTermsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddPaymentTermsCore()
            .AddScoped<ICatalogEntryHandler<PaymentTerm>, PaymentTermHandler>()
            .AddIndexProvider<PaymentTermIndexProvider>()
            .AddDataMigration<PaymentTermMigrations>();

        services
            .AddDisplayDriver<PaymentTerm, PaymentTermDisplayDriver>()
            .AddNavigationProvider<PaymentTermsAdminMenu>()
            .AddPermissionProvider<PaymentTermsPermissionProvider>();

        services.AddRecipeExecutionStep<PaymentTermsStep>();
    }
}
