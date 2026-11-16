using CrestApps.OrchardCore.Timeline.Core.Services;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Timeline.Core;

/// <summary>
/// Registers the Timeline core services.
/// </summary>
public static class TimelineCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the entry store, the timeline service, the access service and the file service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTimelineCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TimelineEntryStore>();
        services.TryAddScoped<ITimelineService, DefaultTimelineService>();
        services.TryAddScoped<ITimelineAccessService, DefaultTimelineAccessService>();
        services.TryAddScoped<ITimelineFileService, DefaultTimelineFileService>();
        services.AddResumableStep<TimelineFileIndexStep>();

        return services;
    }
}
