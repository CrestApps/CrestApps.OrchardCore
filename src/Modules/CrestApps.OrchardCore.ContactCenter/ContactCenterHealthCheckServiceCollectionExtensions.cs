using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Redis;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Contact Center operational health checks.
/// </summary>
/// <remarks>
/// These live apart from <see cref="Startup"/> so a test can register exactly what production registers and
/// then assert the probe predicates select it. Registering the checks inline would leave the tag spelling
/// untested, and a drifted tag makes a probe report healthy with nothing checked.
/// <para>
/// Checks are split by owning feature because a health check must never outlive the services it depends on. A
/// check registered by a feature that does not own its dependencies fails to construct on any tenant that
/// enables the registering feature without the owning one, which turns a probe into an error instead of a
/// verdict.
/// </para>
/// <para>
/// Tags decide which probe a check answers, and the split is deliberate. Only node-local state carries
/// <c>ReadyTag</c>, because readiness drains a node: a check over a shared dependency reports the same verdict
/// on every node, so gating rotation on it turns a degraded dependency into a fleet-wide outage. Dependency
/// checks carry <c>DependencyTag</c> and are alerting signals only.
/// </para>
/// </remarks>
internal static class ContactCenterHealthCheckServiceCollectionExtensions
{
    /// <summary>
    /// Registers the health checks owned by the base Contact Center feature.
    /// </summary>
    /// <param name="services">The service collection to register the checks with.</param>
    /// <returns>The same <paramref name="services"/> so calls can be chained.</returns>
    public static IServiceCollection AddContactCenterHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<ContactCenterHealthCheckOptions>>()
                .Value;

            return new NodeServingStateTracker(
                options.ConsecutiveFailuresBeforeUnready,
                options.ConsecutiveSuccessesBeforeReady);
        });

        services
            .AddHealthChecks()
            .AddCheck<ContactCenterTopologyHealthCheck>(
                ContactCenterConstants.HealthChecks.TopologyCheckName,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.ReadyTag])
            .AddCheck<ContactCenterNodeServingHealthCheck>(
                ContactCenterConstants.HealthChecks.NodeServingCheckName,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.ReadyTag])
            .AddCheck<ContactCenterNodeHealthCheck>(
                ContactCenterConstants.HealthChecks.NodeCheckName,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.ReadyTag])
            .AddCheck<ContactCenterStorageHealthCheck>(
                ContactCenterConstants.HealthChecks.StorageCheckName,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.DependencyTag])
            .AddCheck<ContactCenterOutboxHealthCheck>(
                ContactCenterConstants.HealthChecks.OutboxCheckName,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.DependencyTag])
            .AddCheck<ContactCenterActiveCallsHealthCheck>(
                ContactCenterConstants.HealthChecks.ActiveCallsCheckName,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.DependencyTag]);

        return services;
    }

    /// <summary>
    /// Registers the health checks owned by the Contact Center Queues feature.
    /// </summary>
    /// <param name="services">The service collection to register the checks with.</param>
    /// <returns>The same <paramref name="services"/> so calls can be chained.</returns>
    /// <remarks>
    /// The queue-backlog gauge reads the queue item store, which only the Queues feature registers, so it must be
    /// registered by that feature and never by the base feature.
    /// </remarks>
    public static IServiceCollection AddContactCenterQueuesHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddHealthChecks()
            .AddCheck<ContactCenterQueueBacklogHealthCheck>(
                ContactCenterConstants.HealthChecks.QueueBacklogCheckName,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.DependencyTag]);

        return services;
    }

    /// <summary>
    /// Registers the distributed-dependency health checks that only make sense once Redis backs the deployment.
    /// </summary>
    /// <param name="services">The service collection to register the checks with.</param>
    /// <returns>The same <paramref name="services"/> so calls can be chained.</returns>
    /// <remarks>
    /// The distributed lock, Redis connectivity, and SignalR backplane probes all depend on services that only
    /// the <c>OrchardCore.Redis</c> feature registers. Enabling that feature is not sufficient: Orchard skips
    /// registering <see cref="IRedisService"/> when the Redis configuration string is missing or invalid, so the
    /// probes are registered only once <see cref="IRedisService"/> is actually present. This mirrors how the
    /// Redis lock, bus, and cache sub-features guard their own registrations, and it keeps the mandatory Redis
    /// dependency honest: the probes never register in a state where they could not construct.
    /// </remarks>
    public static IServiceCollection AddContactCenterRedisHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IRedisService)))
        {
            return services;
        }

        services
            .AddHealthChecks()
            .AddCheck<ContactCenterDistributedLockHealthCheck>(
                ContactCenterConstants.HealthChecks.DistributedLockCheckName,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.DependencyTag])
            .AddCheck<ContactCenterRedisConnectivityHealthCheck>(
                ContactCenterConstants.HealthChecks.RedisConnectivityCheckName,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.DependencyTag])
            .AddCheck<ContactCenterBackplaneHealthCheck>(
                ContactCenterConstants.HealthChecks.BackplaneCheckName,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.DependencyTag]);

        return services;
    }

    /// <summary>
    /// Registers the health checks owned by the Contact Center Voice feature.
    /// </summary>
    /// <param name="services">The service collection to register the checks with.</param>
    /// <returns>The same <paramref name="services"/> so calls can be chained.</returns>
    /// <remarks>
    /// The provider ingress check reads the provider webhook inbox store, which only the Voice feature
    /// registers, so it must be registered by that feature and never by the base feature.
    /// </remarks>
    public static IServiceCollection AddContactCenterVoiceHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddHealthChecks()
            .AddCheck<ContactCenterProviderIngressHealthCheck>(
                ContactCenterConstants.HealthChecks.ProviderIngressCheckName,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.DependencyTag]);

        return services;
    }
}
