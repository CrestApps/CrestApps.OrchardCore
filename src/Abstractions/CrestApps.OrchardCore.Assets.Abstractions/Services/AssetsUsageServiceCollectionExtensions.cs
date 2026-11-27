using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Assets.Services;

/// <summary>
/// Registers what other modules add to assets.
/// </summary>
public static class AssetsUsageServiceCollectionExtensions
{
    /// <summary>
    /// Registers how a module tells that its records point to an asset, so the asset can't be deleted.
    /// </summary>
    /// <typeparam name="TUsage">The usage.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddAssetUsage<TUsage>(this IServiceCollection services)
        where TUsage : class, IAssetUsage
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAssetUsage, TUsage>());

        return services;
    }
}
