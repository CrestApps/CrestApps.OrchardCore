using CrestApps.Core.Builders;
using CrestApps.Core.Data.YesSql.Omnichannel.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Omnichannel;

/// <summary>
/// Registers the YesSql persistence behind the omnichannel services.
/// </summary>
/// <remarks>
/// These are kept apart from the services that read through them, so a host that stores its omnichannel data
/// somewhere else registers its own implementations of the same contracts and calls nothing here. The builder
/// method is what a host composing the suite calls; it is sugar over the <c>AddCore*StoresYesSql</c> method
/// beneath it.
/// </remarks>
public static class CoreOmnichannelStoresServiceCollectionExtensions
{
    /// <summary>
    /// Registers the YesSql-backed omnichannel indexes on the omnichannel builder.
    /// </summary>
    /// <param name="builder">The omnichannel builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static CrestAppsOmnichannelBuilder AddYesSqlStores(this CrestAppsOmnichannelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreOmnichannelStoresYesSql();

        return builder;
    }

    /// <summary>
    /// Registers the index providers that keep the omnichannel tables in step with their documents.
    /// </summary>
    /// <remarks>
    /// The contact indexes are not here. They index the host's own contact records - content items in this
    /// repository's host - so only a host can describe them.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreOmnichannelStoresYesSql(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IIndexProvider, OmnichannelActivityIndexProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IIndexProvider, OmnichannelActivityBatchIndexProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IIndexProvider, CadenceIndexProvider>());

        return services;
    }
}
