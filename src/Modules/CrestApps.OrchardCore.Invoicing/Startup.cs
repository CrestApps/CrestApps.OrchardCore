using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Drivers;
using CrestApps.OrchardCore.Invoicing.Endpoints;
using CrestApps.OrchardCore.Invoicing.Migrations;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Navigation;
using CrestApps.OrchardCore.Invoicing.Recipes;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Taxation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Invoicing;

/// <summary>
/// Registers invoices and credit notes: their stores and indexes, the invoice manager and commands, the permissions and
/// the invoicing settings.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddInvoicingCore()
            .AddIndexProvider<InvoiceIndexProvider>()
            .AddIndexProvider<CreditNoteIndexProvider>()
            .AddDataMigration<InvoicingMigrations>()
            .AddPermissionProvider<InvoicingPermissionProvider>()
            .AddSiteDisplayDriver<InvoicingSettingsDisplayDriver>()
            .AddNavigationProvider<InvoicingSettingsAdminMenu>()
            .AddNavigationProvider<InvoicesAdminMenu>()
            .AddDisplayDriver<Invoice, InvoiceDisplayDriver>()
            .AddMoneyFormatter();

        services.AddScoped<ICheckoutReferenceDescriber, InvoiceCheckoutReferenceDescriber>();

        // The invoices and credit notes live in a collection of their own, whose document table is created only when
        // the collection is declared here.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(InvoicingConstants.CollectionName));

        // Lets a role that holds only the settings permission open Settings > Commerce > Invoicing.
        services.AddSiteSettingsPermission(InvoicingConstants.SettingsGroupId, InvoicingPermissions.ManageInvoicingSettings);
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.AddInvoicePartySearchEndpoints();
}

/// <summary>
/// Describes the invoicing settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, InvoicingSettingsSchema>();
    }
}

/// <summary>
/// Taxes invoices from the Taxation module's rules while it is enabled, for the bill-to's party tax profile.
/// </summary>
[RequireFeatures(TaxationConstants.Feature.Taxation)]
public sealed class TaxationStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDocumentTaxProvider<TaxationDocumentTaxProvider>();
    }
}
