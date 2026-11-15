using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Invoicing.Core.Handlers;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CrestApps.OrchardCore.Numbering;

namespace CrestApps.OrchardCore.Invoicing.Core;

/// <summary>
/// Registers the invoicing services.
/// </summary>
public static class InvoicingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the calculator, the invoice and credit note stores, the invoice manager and handler, and the invoice
    /// commands. The ledger, the numbering and the site services come from the features Invoicing depends on.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddInvoicingCore(this IServiceCollection services)
    {
        services.TryAddScoped<IDocumentCalculator, DocumentCalculator>();
        services.TryAddScoped<IInvoiceStore, InvoiceStore>();
        services.TryAddScoped<ICreditNoteStore, CreditNoteStore>();
        services.TryAddScoped<IInvoiceManager, InvoiceManager>();
        services.TryAddScoped<IInvoiceCommandService, DefaultInvoiceCommandService>();
        services.TryAddScoped<ICreditNoteCommandService, DefaultCreditNoteCommandService>();
        services.TryAddScoped<InvoicingDocumentImporter>();

        // Delivery: the printable document, its PDF (archived at issue), share links, sending and viewed tracking. The
        // feature registers the archive's file store (InvoiceArchiveFileStore).
        services.AddSignedLinks();
        services.TryAddScoped<IInvoiceDocumentBuilder, InvoiceDocumentBuilder>();
        services.TryAddScoped<IInvoicePdfService, InvoicePdfService>();
        services.TryAddScoped<IInvoiceShareLinkService, InvoiceShareLinkService>();
        services.TryAddScoped<IInvoiceSender, DefaultInvoiceSender>();
        services.TryAddScoped<InvoiceViewTracker>();

        // What a signed-in customer sees as their own invoices: those billed to a party they have billing access to.
        services.AddPartyMembership();
        services.TryAddScoped<IInvoiceVisibilityService, DefaultInvoiceVisibilityService>();
        services.TryAddScoped<InvoiceObserverNotifier>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IInvoiceObserver, InvoiceArchiveObserver>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Invoice>, InvoiceHandler>());

        // An issued invoice follows its transaction: payments, payments taken back, refunds and settlement.
        services.TryAddScoped<InvoiceLedgerSync>();
        services.TryAddScoped<InvoiceLedgerAvailability>();
        services.TryAddScoped<InvoiceCommandLocks>();
        services.TryAddScoped<IInvoiceLedgerSyncScheduler, DeferredInvoiceLedgerSyncScheduler>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITransactionPaymentHandler, InvoiceTransactionHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITransactionEventHandler, InvoiceTransactionHandler>());

        // Deposits invoiced before the work, deducted from the final invoice.
        services.TryAddScoped<IInvoicePrepaymentService, DefaultInvoicePrepaymentService>();

        // An invoice marked to be charged automatically is charged to the payer's saved card, or sent when it can't be.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IInvoiceObserver, InvoiceAutoCharge>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITransactionEventHandler, InvoiceAutoCharge>());

        return services;
    }

    /// <summary>
    /// Registers collections: the late-fee policies, their store and manager, the late-fee service and the statements.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddInvoicingCollections(this IServiceCollection services)
    {
        services.TryAddScoped<ILateFeePolicyStore, LateFeePolicyStore>();
        services.TryAddScoped<ILateFeePolicyManager, LateFeePolicyManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<LateFeePolicy>, LateFeePolicyHandler>());
        services.TryAddScoped<ILateFeeService, DefaultLateFeeService>();
        services.TryAddScoped<IInvoiceStatementService, DefaultInvoiceStatementService>();

        return services;
    }

    /// <summary>
    /// Registers estimates: their store, manager and handler, the commands, the share links and the number series.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddInvoicingEstimates(this IServiceCollection services)
    {
        services.TryAddScoped<IEstimateStore, EstimateStore>();
        services.TryAddScoped<IEstimateManager, EstimateManager>();
        services.TryAddScoped<IEstimateCommandService, DefaultEstimateCommandService>();
        services.TryAddScoped<EstimateShareLinkService>();
        services.TryAddScoped<EstimateSender>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Estimate>, EstimateHandler>());
        services.AddNumberSeries(
            InvoicingConstants.EstimateNumberSeries,
            global::OrchardCore.Localization.LocalizationSource.Create("Estimates", typeof(InvoicingServiceCollectionExtensions)),
            new CrestApps.OrchardCore.Numbering.NumberSeriesFormat("EST-{seq}", 1001));

        return services;
    }

    /// <summary>
    /// Registers recurring plans: their store, manager and handler, and the service that makes their invoices.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddInvoicingRecurring(this IServiceCollection services)
    {
        services.TryAddScoped<IRecurringPlanStore, RecurringPlanStore>();
        services.TryAddScoped<IRecurringPlanManager, RecurringPlanManager>();
        services.TryAddScoped<IRecurringPlanService, DefaultRecurringPlanService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<RecurringPlan>, RecurringPlanHandler>());

        return services;
    }

    /// <summary>
    /// Registers a tax provider for invoices, estimates and credit notes.
    /// </summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddDocumentTaxProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IDocumentTaxProvider
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDocumentTaxProvider, TProvider>());

        return services;
    }
}
