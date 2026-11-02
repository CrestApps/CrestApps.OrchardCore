using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the time tracker services.
/// </summary>
public static class TimeTrackerServiceCollectionExtensions
{
    /// <summary>
    /// Adds the stores, the managers and the time tracker service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTimeTrackerCore(this IServiceCollection services)
    {
        services.TryAddScoped<TimeCategoryStore>();
        services.TryAddScoped<TimeStageStore>();
        services.TryAddScoped<TimeEntryStore>();
        services.TryAddScoped<TimeCategoryManager>();
        services.TryAddScoped<TimeStageManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TimeCategory>, TimeCategoryHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TimeStage>, TimeStageHandler>());
        services.TryAddScoped<ITimeTrackerService, DefaultTimeTrackerService>();

        return services;
    }
}
