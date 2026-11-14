using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Core.Handlers;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
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
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Invoice>, InvoiceHandler>());

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
