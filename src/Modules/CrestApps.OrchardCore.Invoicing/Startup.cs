using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
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
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.PageSections;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Localization;
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
    private static readonly LocalizationSource _invoiceKindTitle = LocalizationSource.Create<Startup>("Invoices");
    private static readonly LocalizationSource _creditNoteKindTitle = LocalizationSource.Create<Startup>("Credit notes");

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
            .AddNavigationProvider<InvoicesAccountMenu>()
            .AddDisplayDriver<Invoice, InvoiceDisplayDriver>()
            .AddMoneyFormatter();

        services.AddScoped<ICheckoutReferenceDescriber, InvoiceCheckoutReferenceDescriber>();

        // Each document kind has its own template, and each e-mail kind its own message a site can change.
        services
            .AddBusinessDocumentKind(InvoicingConstants.BusinessDocumentKinds.Invoice, _invoiceKindTitle)
            .AddBusinessDocumentKind(InvoicingConstants.BusinessDocumentKinds.CreditNote, _creditNoteKindTitle)
            .AddCommerceEmailKind(
                InvoicingConstants.EmailKinds.InvoiceIssued,
                LocalizationSource.Create<Startup>("Invoice sent to its customer"),
                LocalizationSource.Create<Startup>("{{ Document }}: the invoice, such as {{ Document.Number }}; {{ Number }}, {{ Total }}, {{ AmountDue }}, {{ DueDate }}, {{ PayUrl }}, {{ ViewUrl }}, {{ CustomerName }} and {{ StoreName }}. The PDF is attached."));

        // The PDFs of issued invoices and credit notes, kept privately under the tenant's App_Data folder. A module of its
        // own can register the same store in Azure Blob Storage, as the other .Azure modules do.
        services.AddTenantFileStore<InvoiceArchiveFileStore>(
            "InvoiceArchives",
            configurationSection: null,
            (_, fileStore) => new InvoiceArchiveFileStore(fileStore));

        // The invoices and credit notes live in a collection of their own, whose document table is created only when
        // the collection is declared here.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(InvoicingConstants.CollectionName));

        // Lets a role that holds only the settings permission open Settings > Commerce > Invoicing.
        services.AddSiteSettingsPermission(InvoicingConstants.SettingsGroupId, InvoicingPermissions.ManageInvoicingSettings);
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddInvoicePartySearchEndpoints();

        // The pages a share link opens, without signing in.
        routes.MapAreaControllerRoute(
            name: InvoicingConstants.RouteNames.View,
            areaName: InvoicingConstants.Features.Area,
            pattern: "invoices/view/{token}",
            defaults: new { controller = "Invoice", action = "Show" });

        routes.MapAreaControllerRoute(
            name: InvoicingConstants.RouteNames.ViewPdf,
            areaName: InvoicingConstants.Features.Area,
            pattern: "invoices/view/{token}/pdf",
            defaults: new { controller = "Invoice", action = "Pdf" });
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
/// <summary>
/// Gives an account's page its Invoices section when the Accounts feature is on.
/// </summary>
[RequireFeatures(AccountsConstants.Feature)]
public sealed class AccountsStartup : StartupBase
{
    private static readonly LocalizationSource _sectionTitle = LocalizationSource.Create<AccountsStartup>("Invoices");

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPageSection(AccountPageTabs.PageName, AccountPageTabs.Invoices, _sectionTitle, "fa-solid fa-file-invoice-dollar", AccountPageTabs.GetPosition(AccountPageTabs.Invoices));
        services.AddScoped<IContentDisplayDriver, AccountInvoicesDisplayDriver>();
    }
}

[RequireFeatures(TaxationConstants.Feature.Taxation)]
public sealed class TaxationStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDocumentTaxProvider<TaxationDocumentTaxProvider>();
    }
}
