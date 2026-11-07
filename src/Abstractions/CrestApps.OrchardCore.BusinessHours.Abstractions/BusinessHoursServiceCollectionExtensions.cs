using CrestApps.OrchardCore.BusinessHours.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.BusinessHours;

/// <summary>
/// Registration helpers for modules that keep hours schedules.
/// </summary>
public static class BusinessHoursServiceCollectionExtensions
{
    /// <summary>
    /// Registers a source of hours schedules, so <see cref="IOpenHoursService"/> answers for its schedules.
    /// </summary>
    /// <typeparam name="TSource">The source.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddHoursScheduleSource<TSource>(this IServiceCollection services)
        where TSource : class, IHoursScheduleSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IHoursScheduleSource, TSource>());

        return services;
    }
}
