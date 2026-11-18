using CrestApps.OrchardCore.Attachments.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Attachments;

/// <summary>
/// Registration helpers for modules that build on record attachments.
/// </summary>
public static class AttachmentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers who may read and change the files of one kind of record.
    /// </summary>
    /// <typeparam name="TProvider">The access provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAttachmentAccessProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IAttachmentAccessProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAttachmentAccessProvider, TProvider>());

        return services;
    }

    /// <summary>
    /// Registers a scanner every attached file must pass.
    /// </summary>
    /// <typeparam name="TScanner">The scanner.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAttachmentScanner<TScanner>(this IServiceCollection services)
        where TScanner : class, IAttachmentScanner
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAttachmentScanner, TScanner>());

        return services;
    }
}
