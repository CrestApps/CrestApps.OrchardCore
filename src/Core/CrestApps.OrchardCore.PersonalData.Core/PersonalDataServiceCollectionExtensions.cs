using CrestApps.OrchardCore.PersonalData.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.PersonalData.Core;

/// <summary>
/// Registration helpers for privacy requests.
/// </summary>
public static class PersonalDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the personal-data service that runs every contributor. Calling it more than once is safe.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPersonalData(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IPersonalDataService, DefaultPersonalDataService>();

        return services;
    }

    /// <summary>
    /// Registers a module's personal-data contributor, and the personal-data service when it is not registered yet.
    /// </summary>
    /// <typeparam name="TContributor">The contributor.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPersonalDataContributor<TContributor>(this IServiceCollection services)
        where TContributor : class, IPersonalDataContributor
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddPersonalData();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPersonalDataContributor, TContributor>());

        return services;
    }
}
