using CrestApps.Core.Services;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Assets services.
/// </summary>
public static class AssetsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the asset type and asset stores and managers, meter readings and the asset service.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddAssetsCore(this IServiceCollection services)
    {
        services.TryAddScoped<AssetTypeStore>();
        services.TryAddScoped<AssetTypeManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<AssetType>, AssetTypeHandler>());
        services.TryAddScoped<AssetStore>();
        services.TryAddScoped<AssetManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Asset>, AssetHandler>());
        services.TryAddScoped<AssetMeterReadingStore>();
        services.TryAddScoped<AssetService>();

        return services;
    }
}
