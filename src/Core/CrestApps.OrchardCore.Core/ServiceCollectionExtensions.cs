using CrestApps.Core.Hosting;
using CrestApps.Core.Locking;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Hosting;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Core;

/// <summary>
/// Provides extension methods for service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the catalogs.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddCatalogs(this IServiceCollection services)
    {
        services.TryAddScoped(typeof(ICatalog<>), typeof(Catalog<>));
        services.TryAddScoped(typeof(INamedCatalog<>), typeof(NamedCatalog<>));
        services.TryAddScoped(typeof(ISourceCatalog<>), typeof(SourceCatalog<>));
        services.TryAddScoped(typeof(INamedSourceCatalog<>), typeof(NamedSourceCatalog<>));

        return services;
    }

    /// <summary>
    /// Registers the <see cref="TimeProvider"/> used by the CrestApps services, backed by the Orchard Core clock.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddCoreTimeProvider(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider, ClockTimeProviderAdapter>();

        return services;
    }

    /// <summary>
    /// Binds the framework host seams to their Orchard Core implementations, so framework services
    /// get the tenant's distributed lock and tenant name rather than the single-node defaults.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddCoreHostSeams(this IServiceCollection services)
    {
        services.AddCoreTimeProvider();
        services.TryAddSingleton<IDistributedLockProvider, OrchardCoreDistributedLockProvider>();
        services.TryAddSingleton<ITenantAccessor, ShellSettingsTenantAccessor>();

        return services;
    }
}
