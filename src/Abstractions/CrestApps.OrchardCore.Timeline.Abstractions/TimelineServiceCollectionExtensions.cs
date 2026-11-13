using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Timeline;

/// <summary>
/// Registration helpers for modules that build on the timeline.
/// </summary>
public static class TimelineServiceCollectionExtensions
{
    /// <summary>
    /// Registers who may read and write the timeline of one kind of record.
    /// </summary>
    /// <typeparam name="TProvider">The access provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTimelineAccessProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ITimelineAccessProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITimelineAccessProvider, TProvider>());

        return services;
    }

    /// <summary>
    /// Registers a handler that takes part in adding timeline entries.
    /// </summary>
    /// <typeparam name="THandler">The handler.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTimelineEntryHandler<THandler>(this IServiceCollection services)
        where THandler : class, ITimelineEntryHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITimelineEntryHandler, THandler>());

        return services;
    }
}
