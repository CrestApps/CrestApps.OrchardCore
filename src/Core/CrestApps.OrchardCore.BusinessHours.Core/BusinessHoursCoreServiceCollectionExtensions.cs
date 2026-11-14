using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.BusinessHours.Core;

/// <summary>
/// Registers the open-hours service and the schedules the Business Hours module keeps.
/// </summary>
public static class BusinessHoursCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IOpenHoursService"/> once, whichever feature asks first. Every feature that asks "are we
    /// open?" or keeps schedules calls it; schedules come from the <see cref="IHoursScheduleSource"/>s features register.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddOpenHours(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IOpenHoursService, DefaultOpenHoursService>();

        return services;
    }

    /// <summary>
    /// Registers the rules of the <see cref="BusinessHoursSchedule"/> records and offers them to the open-hours service.
    /// The caller registers the catalogs and their managers, which store the records.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddBusinessHoursSchedules(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOpenHours();
        services.AddHoursScheduleSource<BusinessHoursScheduleSource>();

        return services;
    }
}
