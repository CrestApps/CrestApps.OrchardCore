using CrestApps.Core.Builders;
using CrestApps.Core.Hosting;
using CrestApps.Core.Hosting.Locking;
using CrestApps.Core.Locking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.Core;

/// <summary>
/// Registers the host seams the Contact Center Suite depends on, and opens the suite a host composes
/// its pillars from.
/// </summary>
/// <remarks>
/// The class is named for what it registers rather than <c>ServiceCollectionExtensions</c>, because
/// <c>CrestApps.Core</c> already declares a class by that name in this namespace and this project folds
/// into it.
/// </remarks>
public static class HostingServiceCollectionExtensions
{
    /// <summary>
    /// Opens the Contact Center Suite, registering the host seams every pillar needs and nothing else.
    /// </summary>
    /// <remarks>
    /// The pillars - telephony, omnichannel, the contact centre itself, the SMS portal - are turned on
    /// individually on the builder this hands back, so a host that wants one of them does not get the
    /// rest. This mirrors <c>AddAISuite</c>.
    /// </remarks>
    /// <param name="builder">The framework builder.</param>
    /// <param name="configure">An optional delegate used to turn on the pillars the host wants.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsCoreBuilder AddContactCenterSuite(
        this CrestAppsCoreBuilder builder,
        Action<CrestAppsContactCenterSuiteBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreHosting();

        configure?.Invoke(new CrestAppsContactCenterSuiteBuilder(builder.Services));

        return builder;
    }

    /// <summary>
    /// Registers the default host seams: an in-process distributed lock, a single-tenant accessor, and a
    /// service-provider scope executor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every default is registered with <c>TryAdd</c>, so a host that knows better can register its
    /// own implementation first and keep it.
    /// </para>
    /// <para>
    /// <see cref="IAfterCommitTaskQueue"/> is deliberately not among them. Work put on that queue runs
    /// after the unit of work commits, and what commits a unit of work is the store package the host
    /// chose - so the thing that drains the queue belongs with the store, not here. Registering
    /// <see cref="AfterCommitTaskQueue"/> with nothing to drain it would accept work and silently drop
    /// it, which is worse than the resolve failing: a host that has no queue finds out at startup. The
    /// implementation is public so a host that has a commit point can register and drain it.
    /// </para>
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreHosting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDistributedLockProvider, LocalDistributedLockProvider>();
        services.TryAddSingleton<ITenantAccessor, SingleTenantAccessor>();
        services.TryAddSingleton<IScopedWorkExecutor, ServiceProviderScopedWorkExecutor>();
        services.TryAddSingleton<IDetachedWorkExecutor, ServiceProviderDetachedWorkExecutor>();

        return services;
    }
}
