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
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IInvoiceObserver, InvoiceArchiveObserver>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Invoice>, InvoiceHandler>());

        // An issued invoice follows its transaction: payments, payments taken back, refunds and settlement.
        services.TryAddScoped<InvoiceLedgerSync>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITransactionPaymentHandler, InvoiceTransactionHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITransactionEventHandler, InvoiceTransactionHandler>());

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
