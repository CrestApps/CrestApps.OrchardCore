using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkQueues;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using YesSql.Indexes;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers leased work queues.
/// </summary>
public static class LeasedWorkQueueServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="ILeasedWorkQueue{T}"/> of a work item type on the shared <see cref="LeasedWorkItemIndex"/>,
    /// and the index provider that maps the type to it. The feature's migration creates the index table of the
    /// collection with <see cref="LeasedWorkItemIndexSchema.CreateAsync"/>.
    /// </summary>
    /// <typeparam name="T">The work item type.</typeparam>
    /// <param name="services">The tenant's services.</param>
    /// <param name="configure">Sets the collection, the attempts and whether completed items are removed.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddLeasedWorkQueue<T>(this IServiceCollection services, Action<LeasedWorkQueueOptions> configure)
        where T : CatalogItem, ILeasedWorkItem
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure<LeasedWorkQueueOptions<T>>(options => configure(options));
        services.TryAddScoped<ILeasedWorkQueue<T>, LeasedWorkQueue<T>>();
        services.AddSingleton<IIndexProvider>(serviceProvider =>
            new LeasedWorkItemIndexProvider<T>(serviceProvider.GetRequiredService<IOptions<LeasedWorkQueueOptions<T>>>().Value.CollectionName));

        return services;
    }
}
