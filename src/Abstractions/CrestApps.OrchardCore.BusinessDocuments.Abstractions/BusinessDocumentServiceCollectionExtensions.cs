using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.BusinessDocuments;

/// <summary>
/// Registers the kinds of business documents a module prints.
/// </summary>
public static class BusinessDocumentServiceCollectionExtensions
{
    /// <summary>
    /// Registers a kind of business document, so the template editor offers it and templates can be limited to it.
    /// The module that prints the kind registers it; registering a name again replaces it.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="name">The name of the kind, such as <c>Invoice</c>.</param>
    /// <param name="title">The title shown for the kind.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddBusinessDocumentKind(this IServiceCollection services, string name, LocalizationSource title)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        services.Configure<BusinessDocumentOptions>(options => options.Kinds[name] = new BusinessDocumentKind
        {
            Name = name,
            Title = title,
        });

        return services;
    }
}
