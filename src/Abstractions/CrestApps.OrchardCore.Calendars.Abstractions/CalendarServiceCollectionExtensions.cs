using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registration helpers for modules that extend the calendar.
/// </summary>
public static class CalendarServiceCollectionExtensions
{
    /// <summary>
    /// Registers a calendar type, or extends one another feature registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The technical name of the type, stored on each calendar of the type.</param>
    /// <param name="configure">Configures the type.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalendarType(this IServiceCollection services, string name, Action<CalendarTypeBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure<CalendarOptions>(options => configure(new CalendarTypeBuilder(options.GetOrAddType(name))));

        return services;
    }

    /// <summary>
    /// Registers a source of busy time that free/busy and scheduling take into account.
    /// </summary>
    /// <typeparam name="TSource">The source's type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalendarBusyTimeSource<TSource>(this IServiceCollection services)
        where TSource : class, ICalendarBusyTimeSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICalendarBusyTimeSource, TSource>();

        return services;
    }

    /// <summary>
    /// Registers a source of read-only occurrences the calendar view shows next to stored events.
    /// </summary>
    /// <typeparam name="TSource">The source's type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalendarEventSource<TSource>(this IServiceCollection services)
        where TSource : class, ICalendarEventSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICalendarEventSource, TSource>();

        return services;
    }
}
