using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.ServiceLevels;

/// <summary>
/// Registration helpers for service levels.
/// </summary>
public static class ServiceLevelsServiceCollectionExtensions
{
    /// <summary>
    /// Registers an observer of service level timers.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddServiceLevelObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IServiceLevelObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IServiceLevelObserver, TObserver>());

        return services;
    }

    /// <summary>
    /// Registers a kind of work that service level policies are written for.
    /// </summary>
    /// <typeparam name="TProvider">The scope provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddServiceLevelScope<TProvider>(this IServiceCollection services)
        where TProvider : class, IServiceLevelScopeProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IServiceLevelScopeProvider, TProvider>());

        return services;
    }
}
