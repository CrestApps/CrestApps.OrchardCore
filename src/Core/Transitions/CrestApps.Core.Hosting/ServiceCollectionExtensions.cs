using CrestApps.Core.Hosting;
using CrestApps.Core.Hosting.Locking;
using CrestApps.Core.Locking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.Core;

/// <summary>
/// Registers the host seams the Contact Center Suite depends on.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default host seams: an in-process distributed lock, a single-tenant accessor, a
    /// service-provider scope executor, and an after-commit work queue.
    /// </summary>
    /// <remarks>
    /// Every default is registered with <c>TryAdd</c>, so a host that knows better can register its
    /// own implementation first and keep it.
    /// </remarks>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddCoreHosting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDistributedLockProvider, LocalDistributedLockProvider>();
        services.TryAddSingleton<ITenantAccessor, SingleTenantAccessor>();
        services.TryAddSingleton<IScopedWorkExecutor, ServiceProviderScopedWorkExecutor>();
        services.TryAddScoped<IAfterCommitTaskQueue, AfterCommitTaskQueue>();

        return services;
    }
}
