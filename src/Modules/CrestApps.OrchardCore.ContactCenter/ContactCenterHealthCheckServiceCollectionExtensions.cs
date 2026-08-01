using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
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
            .AddCheck<ContactCenterDistributedLockHealthCheck>(
                ContactCenterConstants.HealthChecks.DistributedLockCheckName,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.DependencyTag])
            .Add(new HealthCheckRegistration(
                ContactCenterConstants.HealthChecks.RedisConnectivityCheckName,
                serviceProvider => new ContactCenterRedisConnectivityHealthCheck(serviceProvider.GetService<IRedisService>()),
                failureStatus: null,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.DependencyTag]))
            .Add(new HealthCheckRegistration(
                ContactCenterConstants.HealthChecks.BackplaneCheckName,
                serviceProvider => new ContactCenterBackplaneHealthCheck(
                    serviceProvider.GetRequiredService<IOptions<RedisOptions>>(),
                    serviceProvider.GetRequiredService<ShellSettings>(),
                    serviceProvider.GetService<IRedisService>()),
                failureStatus: null,
                tags: [ContactCenterConstants.HealthChecks.AreaTag, ContactCenterConstants.HealthChecks.DependencyTag]));

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
