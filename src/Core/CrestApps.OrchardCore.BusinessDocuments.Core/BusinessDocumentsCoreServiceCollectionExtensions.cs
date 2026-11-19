using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.BusinessDocuments.Core;

/// <summary>
/// Registers the business documents services.
/// </summary>
public static class BusinessDocumentsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the document template store and manager and the HTML renderer. It is safe to call more than once.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddBusinessDocumentsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<BusinessDocumentOptions>();
        services.TryAddScoped<IBusinessDocumentTemplateStore, BusinessDocumentTemplateStore>();
        services.TryAddScoped<IBusinessDocumentTemplateManager, BusinessDocumentTemplateManager>();
        services.TryAddScoped<ICatalogManager<BusinessDocumentTemplate>>(serviceProvider => serviceProvider.GetRequiredService<IBusinessDocumentTemplateManager>());
        services.TryAddScoped<IBusinessDocumentHtmlRenderer, BusinessDocumentHtmlRenderer>();

        return services;
    }
}
