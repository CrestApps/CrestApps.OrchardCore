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

    /// <summary>
    /// Registers a provider that says which of a module's records use a policy, so a policy in use isn't deleted.
    /// </summary>
    /// <typeparam name="TProvider">The usage provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddServiceLevelPolicyUsage<TProvider>(this IServiceCollection services)
        where TProvider : class, IServiceLevelPolicyUsageProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IServiceLevelPolicyUsageProvider, TProvider>());

        return services;
    }

    /// <summary>
    /// Registers who may see the service-level timers of a kind of record.
    /// </summary>
    /// <typeparam name="TProvider">The access provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddServiceLevelTimerAccessProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IServiceLevelTimerAccessProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IServiceLevelTimerAccessProvider, TProvider>());

        return services;
    }
}
