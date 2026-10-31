using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// Registration helpers for external calendar synchronization and its providers.
/// </summary>
public static class CalendarSyncServiceCollectionExtensions
{
    /// <summary>
    /// Registers the synchronization service and the observer that queues local changes for two-way subscriptions.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalendarSync(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ICalendarImporter>();
        services.TryAddScoped<CalendarSyncService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICalendarEventObserver, CalendarSyncChangeObserver>());

        return services;
    }

    /// <summary>
    /// Registers an external calendar provider.
    /// </summary>
    /// <typeparam name="TProvider">The provider's type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalendarProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ICalendarProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCalendarSync();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICalendarProvider, TProvider>());

        return services;
    }
}
