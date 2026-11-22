using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.FileStorage.ProtectedFiles;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.FileStorage;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers protected file areas.
/// </summary>
public static class ProtectedFileServiceCollectionExtensions
{
    /// <summary>
    /// Registers an area of protected files, with its signed links. The files are kept in a folder of the tenant's
    /// App_Data, or in a folder of the shared <see cref="ProtectedFileAreaStore"/> when a feature registers one with
    /// <c>AddTenantFileStore</c>.
    /// </summary>
    /// <typeparam name="TArea">The marker type that names the area.</typeparam>
    /// <param name="services">The services.</param>
    /// <param name="folder">The folder of the area: under the tenant's App_Data, or in the shared store.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddProtectedFileArea<TArea>(this IServiceCollection services, string folder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(folder);

        services.Configure<ProtectedFileAreaOptions<TArea>>(options => options.Folder = folder);
        services.TryAddTransient<FileCreationService>();
        services.TryAddSingleton(ProtectedFileAreaStore.Local);
        services.TryAddScoped<IProtectedFileStore<TArea>>(serviceProvider =>
        {
            var sharedStore = serviceProvider.GetRequiredService<ProtectedFileAreaStore>().SharedStore;
            var fileCreationService = serviceProvider.GetRequiredService<FileCreationService>();

            return sharedStore is null
                ? new ProtectedFileStore<TArea>(TenantFileStoreServiceCollectionExtensions.CreateLocalStore(serviceProvider, folder), fileCreationService)
                : new ProtectedFileStore<TArea>(sharedStore, folder, fileCreationService);
        });
        services.TryAddScoped<IProtectedFileLinks<TArea>, ProtectedFileLinks<TArea>>();

        return services;
    }
}
