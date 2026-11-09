using CrestApps.OrchardCore.BusinessHours.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.BusinessHours.Core;

/// <summary>
/// Registers the open-hours service.
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
}
