using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracking;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers agents' states in Time Tracker.
/// </summary>
public static class ContactCenterTimeTrackerServiceCollectionExtensions
{
    /// <summary>
    /// Adds the sync of agents' states, the stage seeder and the clock Time Tracker names their entries after. Time
    /// Tracker's services and the agent state history must be added too.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddWorkforceContactCenterTimeTracker(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ContactCenterTimeTrackerSync>();
        services.TryAddScoped<ContactCenterTimeStageSeeder>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IExternalTimeClock, ContactCenterExternalClock>());

        return services;
    }
}
