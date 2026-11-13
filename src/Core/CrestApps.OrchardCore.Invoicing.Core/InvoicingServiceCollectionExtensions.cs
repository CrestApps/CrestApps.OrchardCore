using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Invoicing.Core;

/// <summary>
/// Registration helpers for the Invoicing core services.
/// </summary>
public static class InvoicingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the document calculator. Tax providers are added with <see cref="AddDocumentTaxProvider{TProvider}"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddInvoicingCore(this IServiceCollection services)
    {
        services.TryAddScoped<IDocumentCalculator, DocumentCalculator>();

        return services;
    }

    /// <summary>
    /// Registers a provider that sets the taxes of a document's lines from tax rules.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddDocumentTaxProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IDocumentTaxProvider
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDocumentTaxProvider, TProvider>());

        return services;
    }
}
