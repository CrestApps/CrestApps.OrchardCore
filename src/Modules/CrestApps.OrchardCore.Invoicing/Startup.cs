using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.ModelBinding;
using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Deployments;
using CrestApps.OrchardCore.Invoicing.Drivers;
using CrestApps.OrchardCore.Invoicing.Endpoints;
using CrestApps.OrchardCore.Invoicing.Migrations;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Navigation;
using CrestApps.OrchardCore.Invoicing.Recipes;
using CrestApps.OrchardCore.Invoicing.Recipes.Workflows;
using CrestApps.OrchardCore.Invoicing.Reports;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.Workflows;
using CrestApps.OrchardCore.PageSections;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Workflows.Helpers;
using CrestApps.OrchardCore.Invoicing.ViewModels;

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
        // Line quantities, prices and deposit amounts are posted as "24.5" in every culture.
        services.AddInvariantNumberInputs();

        // The Taxation module, which Invoicing depends on, sets the taxes of a document's lines from its rules.
        services.AddDocumentTaxProvider<TaxationDocumentTaxProvider>();
        services.AddScoped<DocumentLinesEditorContext>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, InvoicingResourceManagementOptionsConfiguration>();

        services
            .AddInvoicingCore()
            .AddRecipeExecutionStep<InvoicesStep>()
            .AddRecipeExecutionStep<CreditNotesStep>()
            .AddScoped<CrestApps.OrchardCore.Customers.PersonalData.ICommercePersonalDataContributor, InvoicingPersonalDataContributor>()
            .AddIndexProvider<InvoiceIndexProvider>()
            .AddIndexProvider<CreditNoteIndexProvider>()
            .AddDataMigration<InvoicingMigrations>()
            .AddPermissionProvider<InvoicingPermissionProvider>()
            .AddSiteDisplayDriver<InvoicingSettingsDisplayDriver>()
            .AddNavigationProvider<InvoicingSettingsAdminMenu>()
            .AddNavigationProvider<InvoicesAdminMenu>()
            .AddNavigationProvider<InvoicesAccountMenu>()
            .AddDisplayDriver<Invoice, InvoiceDisplayDriver>()
            .AddDisplayDriver<Invoice, InvoiceDepositsDisplayDriver>()
            .AddDisplayDriver<InvoiceEditViewModel, InvoiceEditDisplayDriver>()
            .AddDisplayDriver<InvoiceSendViewModel, InvoiceSendDisplayDriver>()
            .AddDisplayDriver<CreditNoteEditViewModel, CreditNoteEditDisplayDriver>()
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
/// Starts workflows from what happens to invoices, and gives workflows the invoice tasks, when Workflows is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IInvoiceObserver, InvoiceWorkflowObserver>();

        services.AddActivity<InvoiceCreatedEvent, InvoiceEventDisplayDriver<InvoiceCreatedEvent>>();
        services.AddActivity<InvoiceIssuedEvent, InvoiceEventDisplayDriver<InvoiceIssuedEvent>>();
        services.AddActivity<InvoiceSentEvent, InvoiceEventDisplayDriver<InvoiceSentEvent>>();
        services.AddActivity<InvoiceViewedEvent, InvoiceEventDisplayDriver<InvoiceViewedEvent>>();
        services.AddActivity<InvoicePaymentReceivedEvent, InvoiceEventDisplayDriver<InvoicePaymentReceivedEvent>>();
        services.AddActivity<InvoicePaidEvent, InvoiceEventDisplayDriver<InvoicePaidEvent>>();
        services.AddActivity<InvoiceOverdueEvent, InvoiceEventDisplayDriver<InvoiceOverdueEvent>>();
        services.AddActivity<InvoiceVoidedEvent, InvoiceEventDisplayDriver<InvoiceVoidedEvent>>();
        services.AddActivity<CreditNoteIssuedEvent, InvoiceEventDisplayDriver<CreditNoteIssuedEvent>>();

        services.AddActivity<CreateInvoiceTask, InvoiceTaskDisplayDriver<CreateInvoiceTask>>();
        services.AddActivity<IssueInvoiceTask, InvoiceTaskDisplayDriver<IssueInvoiceTask>>();
        services.AddActivity<SendInvoiceTask, InvoiceTaskDisplayDriver<SendInvoiceTask>>();
        services.AddActivity<RecordInvoicePaymentTask, InvoiceTaskDisplayDriver<RecordInvoicePaymentTask>>();
        services.AddActivity<VoidInvoiceTask, InvoiceTaskDisplayDriver<VoidInvoiceTask>>();
        services.AddActivity<CreateCreditNoteTask, InvoiceTaskDisplayDriver<CreateCreditNoteTask>>();
    }
}

/// <summary>
/// Describes the invoice workflow events and tasks to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<InvoiceCreatedEventSchema>()
            .AddWorkflowActivitySchema<InvoiceIssuedEventSchema>()
            .AddWorkflowActivitySchema<InvoiceSentEventSchema>()
            .AddWorkflowActivitySchema<InvoiceViewedEventSchema>()
            .AddWorkflowActivitySchema<InvoicePaymentReceivedEventSchema>()
            .AddWorkflowActivitySchema<InvoicePaidEventSchema>()
            .AddWorkflowActivitySchema<InvoiceOverdueEventSchema>()
            .AddWorkflowActivitySchema<InvoiceVoidedEventSchema>()
            .AddWorkflowActivitySchema<CreditNoteIssuedEventSchema>()
            .AddWorkflowActivitySchema<CreateInvoiceTaskSchema>()
            .AddWorkflowActivitySchema<IssueInvoiceTaskSchema>()
            .AddWorkflowActivitySchema<SendInvoiceTaskSchema>()
            .AddWorkflowActivitySchema<RecordInvoicePaymentTaskSchema>()
            .AddWorkflowActivitySchema<VoidInvoiceTaskSchema>()
            .AddWorkflowActivitySchema<CreateCreditNoteTaskSchema>();
    }
}

/// <summary>
/// Tells webhooks what happens to invoices when the Commerce Webhooks feature is on.
/// </summary>
[RequireFeatures(CommerceConstants.Features.Webhooks)]
public sealed class WebhooksStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IInvoiceObserver, InvoiceWebhookObserver>();

        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.InvoiceCreated, "Invoice created", "Invoicing");
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.InvoiceIssued, "Invoice issued", "Invoicing");
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.InvoiceSent, "Invoice sent", "Invoicing");
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.InvoiceViewed, "Invoice viewed", "Invoicing");
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.InvoicePaymentReceived, "Invoice payment received", "Invoicing");
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.InvoicePaid, "Invoice paid", "Invoicing");
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.InvoiceOverdue, "Invoice overdue", "Invoicing");
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.InvoiceVoided, "Invoice voided", "Invoicing");
        services.AddWebhookEvent(InvoicingConstants.WebhookEvents.CreditNoteIssued, "Credit note issued", "Invoicing");
    }
}

/// <summary>
/// Exposes invoices, their lines and taxes, and credit notes as a data source for reports and data pipelines.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<InvoicingReportDataSource>()
            .AddScoped<IInvoicingReportDataSet, InvoicesReportDataSet>()
            .AddScoped<IInvoicingReportDataSet, InvoiceLinesReportDataSet>()
            .AddScoped<IInvoicingReportDataSet, InvoiceTaxesReportDataSet>()
            .AddScoped<IInvoicingReportDataSet, CreditNotesReportDataSet>();
    }
}

/// <summary>
/// Adds the invoicing reports to Reports > Commerce.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<InvoicingReportReader>();
        services
            .AddReport<ReceivablesAgingReport>()
            .AddReport<InvoiceRegisterReport>()
            .AddReport<PaymentsCollectedReport>()
            .AddReport<SalesByCustomerReport>()
            .AddReport<SalesByItemReport>()
            .AddReport<TaxSummaryReport>();
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
        services.AddScoped<IRecipeStep, InvoicesRecipeStepSchema>();
        services.AddScoped<IRecipeStep, CreditNotesRecipeStepSchema>();
        services.AddDeploymentStepSchema<InvoicesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<CreditNotesDeploymentStepSchema>();
    }
}

/// <summary>
/// Invoices and credit notes travel with deployment plans.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<InvoicesDeploymentSource, InvoicesDeploymentStep>();
        services.AddDisplayDriver<DeploymentStep, InvoicesDeploymentStepDisplayDriver>();
        services.AddDeployment<CreditNotesDeploymentSource, CreditNotesDeploymentStep, SummaryDeploymentStepDisplayDriver<CreditNotesDeploymentStep>>();
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

/// <summary>
/// Bills completed work orders as invoices when Work Orders is on.
/// </summary>
[RequireFeatures(WorkOrdersConstants.Features.WorkOrders)]
public sealed class WorkOrdersStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IWorkOrderBilling, InvoicingWorkOrderBilling>();
}
