using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Scheduling.Core;

/// <summary>
/// Registers the scheduling services.
/// </summary>
public static class SchedulingCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the scheduling stores, availability and booking services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSchedulingCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<SchedulingResourceStore>();
        services.TryAddScoped<AppointmentTypeStore>();
        services.TryAddScoped<AppointmentStore>();
        services.TryAddScoped<WaitlistStore>();
        services.TryAddScoped<AppointmentChangeQueue>();
        services.TryAddScoped<ISchedulingAvailabilityService, DefaultSchedulingAvailabilityService>();
        services.TryAddScoped<IAppointmentService, DefaultAppointmentService>();

        return services;
    }
}
