using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Drivers;
using CrestApps.OrchardCore.Invoicing.Migrations;
using CrestApps.OrchardCore.Invoicing.Navigation;
using CrestApps.OrchardCore.Invoicing.Recipes;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Taxation;
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
            .AddNavigationProvider<InvoicingSettingsAdminMenu>();

        // Lets a role that holds only the settings permission open Settings > Commerce > Invoicing.
        services.AddSiteSettingsPermission(InvoicingConstants.SettingsGroupId, InvoicingPermissions.ManageInvoicingSettings);
    }
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
