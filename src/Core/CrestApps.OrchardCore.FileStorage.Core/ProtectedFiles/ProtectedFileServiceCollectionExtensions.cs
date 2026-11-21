using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.FileStorage.ProtectedFiles;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.FileStorage;
using OrchardCore.Modules;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers protected file areas.
/// </summary>
public static class ProtectedFileServiceCollectionExtensions
{
    /// <summary>
    /// Registers an area of protected files, with its signed links. The files are kept in a folder of the tenant's
    /// App_Data, or wherever the registered <see cref="IProtectedFileAreaStoreFactory"/> keeps them.
    /// </summary>
    /// <typeparam name="TArea">The marker type that names the area.</typeparam>
    /// <param name="services">The services.</param>
    /// <param name="folder">The folder of the area: under the tenant's App_Data, or under the base path of a remote store.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddProtectedFileArea<TArea>(this IServiceCollection services, string folder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(folder);

        services.Configure<ProtectedFileAreaOptions<TArea>>(options => options.Folder = folder);
        services.TryAddTransient<FileCreationService>();
        services.TryAddSingleton<LocalProtectedFileAreaStoreFactory>();
        services.TryAddSingleton<IProtectedFileAreaStoreFactory>(serviceProvider => serviceProvider.GetRequiredService<LocalProtectedFileAreaStoreFactory>());
        services.TryAddScoped<IProtectedFileStore<TArea>>(serviceProvider =>
        {
            var areaFolder = serviceProvider.GetRequiredService<IOptions<ProtectedFileAreaOptions<TArea>>>().Value.Folder;

            return new ProtectedFileStore<TArea>(
                serviceProvider.GetRequiredService<IProtectedFileAreaStoreFactory>().GetStore(areaFolder),
                serviceProvider.GetRequiredService<FileCreationService>());
        });
        services.TryAddScoped<IProtectedFileLinks<TArea>, ProtectedFileLinks<TArea>>();

        return services;
    }

    /// <summary>
    /// Keeps every protected file area in one Azure Blob Storage container, each area in a folder of its own, when
    /// <paramref name="configurationSection"/> names a connection string and a container. Otherwise the areas stay in
    /// the tenant's App_Data and an error is logged.
    /// </summary>
    /// <param name="services">The tenant's services.</param>
    /// <param name="configurationSection">The shell configuration section the Azure Blob Storage options are read from.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddProtectedFilesAzureBlobStorage(this IServiceCollection services, string configurationSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(configurationSection);

        services.RemoveAll<TenantFileStoreRegistration<ProtectedFileAreas>>();
        services.AddSingleton(new TenantFileStoreRegistration<ProtectedFileAreas>("ProtectedFiles", configurationSection, removeOnTenantRemoval: true));
        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<TenantStoreBlobStorageOptions<ProtectedFileAreas>>, TenantBlobStorageOptionsConfiguration<ProtectedFileAreas>>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IModularTenantEvents, TenantBlobContainerEvents<ProtectedFileAreas>>());
        services.TryAddSingleton<LocalProtectedFileAreaStoreFactory>();
        services.Replace(ServiceDescriptor.Singleton<IProtectedFileAreaStoreFactory, BlobProtectedFileAreaStoreFactory>());

        return services;
    }
}
