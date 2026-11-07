using CrestApps.OrchardCore.Retention;
using CrestApps.OrchardCore.YesSql.Core.Retention;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers retention policies and the one task that runs them.
/// </summary>
public static class RetentionServiceCollectionExtensions
{
    /// <summary>
    /// Registers a retention policy, and the retention task once.
    /// </summary>
    /// <typeparam name="TPolicy">The policy type.</typeparam>
    /// <param name="services">The tenant's services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddRetentionPolicy<TPolicy>(this IServiceCollection services)
        where TPolicy : class, IRetentionPolicy
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<RetentionOptions>().BindConfiguration("CrestApps:Retention");
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRetentionPolicy, TPolicy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, RetentionBackgroundTask>());

        return services;
    }
}
