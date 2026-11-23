using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the pieces other modules add to mailing lists.
/// </summary>
public static class MailingListsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a mailing list provider.
    /// </summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMailingListProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IMailingListProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IMailingListProvider, TProvider>());

        return services;
    }

    /// <summary>
    /// Registers a mailing list member observer.
    /// </summary>
    /// <typeparam name="TObserver">The observer type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMailingListMemberObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IMailingListMemberObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IMailingListMemberObserver, TObserver>());

        return services;
    }
}
