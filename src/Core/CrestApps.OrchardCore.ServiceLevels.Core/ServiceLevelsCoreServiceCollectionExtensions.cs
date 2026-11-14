using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.ServiceLevels.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.ServiceLevels.Core;

/// <summary>
/// Registers the Service Levels core services.
/// </summary>
public static class ServiceLevelsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the policy and timer stores, the policy manager, the tracker and the monitor. The caller also
    /// registers an open-hours service (Business Hours), which timers are counted on.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddServiceLevelsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ServiceLevelPolicyStore>();
        services.TryAddScoped<ServiceLevelTimerStore>();
        services.TryAddScoped<IServiceLevelTimerStore>(provider => provider.GetRequiredService<ServiceLevelTimerStore>());
        services.TryAddScoped<IServiceLevelPolicyManager, ServiceLevelPolicyManager>();
        services.TryAddScoped<IServiceLevelPolicyLookup, ServiceLevelPolicyLookup>();
        services.TryAddScoped<ServiceLevelClock>();
        services.TryAddScoped<IServiceLevelTracker, DefaultServiceLevelTracker>();
        services.TryAddScoped<ServiceLevelMonitor>();
        services.TryAddScoped<ServiceLevelScheduleObserver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessHoursScheduleObserver, ServiceLevelScheduleObserver>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IServiceLevelPolicyUsageProvider, ActiveTimerPolicyUsage>());

        return services;
    }
}
