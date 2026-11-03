using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Calendars.Core;

/// <summary>
/// Registers the calendar services.
/// </summary>
public static class CalendarsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the calendar stores, managers, query, free/busy and access services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalendarsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<CalendarOptions>();
        services.TryAddSingleton<ICalendarTypeManager, DefaultCalendarTypeManager>();
        services.TryAddScoped<ICalendarStore, CalendarStore>();
        services.TryAddScoped<ICalendarEventStore, CalendarEventStore>();
        services.TryAddScoped<ICalendarAccessService, DefaultCalendarAccessService>();
        services.TryAddScoped<ICalendarCategoryService, DefaultCalendarCategoryService>();
        services.TryAddScoped<ICalendarService, DefaultCalendarService>();
        services.TryAddScoped<ICalendarQueryService, DefaultCalendarQueryService>();
        services.TryAddScoped<ICalendarAvailabilityService, DefaultCalendarAvailabilityService>();
        services.TryAddScoped<ICalendarEventManager, DefaultCalendarEventManager>();
        services.TryAddScoped<CalendarConflictChecker>();
        services.TryAddScoped<CalendarChangeQueue>();

        return services;
    }
}
