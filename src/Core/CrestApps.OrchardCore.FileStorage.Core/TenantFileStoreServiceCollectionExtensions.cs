using CrestApps.OrchardCore.FileStorage;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.FileStorage;
using OrchardCore.FileStorage.AzureBlob;
using OrchardCore.FileStorage.FileSystem;
using OrchardCore.Modules;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers a tenant file store: a folder of the tenant's own application data, or a container in Azure Blob Storage
/// when the shell configuration names one.
/// </summary>
public static class TenantFileStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TStore"/> as a singleton built on a tenant file store.
    /// </summary>
    /// <remarks>
    /// When <paramref name="configurationSection"/> holds a connection string and a container name, the store keeps
    /// its files in Azure Blob Storage, the container is created when the tenant starts, and it is removed with the
    /// tenant when <paramref name="removeOnTenantRemoval"/> and the options say so. Otherwise the store keeps the
    /// registration it replaces, such as the parent feature's local store, or, when there is none, keeps its files
    /// in <c>App_Data/Sites/{tenant}/{purpose}</c>. A feature that only moves a store to Azure therefore calls this
    /// once, with the same section name its sites already configure.
    /// </remarks>
    /// <typeparam name="TStore">The store type.</typeparam>
    /// <param name="services">The tenant's services.</param>
    /// <param name="purpose">The folder, inside the tenant's application data, the store keeps its files in locally.</param>
    /// <param name="configurationSection">
    /// The shell configuration section the Azure Blob Storage options are read from, or <see langword="null"/> for a
    /// store that is always local.
    /// </param>
    /// <param name="factory">Builds the store over the file store chosen for it.</param>
    /// <param name="removeOnTenantRemoval">Whether the store's container or base path may be removed with the tenant.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTenantFileStore<TStore>(
        this IServiceCollection services,
        string purpose,
        string configurationSection,
        Func<IServiceProvider, IFileStore, TStore> factory,
        bool removeOnTenantRemoval = false)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(purpose);
        ArgumentNullException.ThrowIfNull(factory);

        var registration = new TenantFileStoreRegistration<TStore>(purpose, configurationSection, removeOnTenantRemoval);
        var replaced = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(TStore) && !descriptor.IsKeyedService);

        services.RemoveAll<TenantFileStoreRegistration<TStore>>();
        services.AddSingleton(registration);

        if (!string.IsNullOrEmpty(configurationSection))
        {
            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<TenantStoreBlobStorageOptions<TStore>>, TenantBlobStorageOptionsConfiguration<TStore>>());
            services.TryAddEnumerable(ServiceDescriptor.Scoped<IModularTenantEvents, TenantBlobContainerEvents<TStore>>());
        }

        services.Replace(ServiceDescriptor.Singleton<TStore>(serviceProvider =>
        {
            if (!string.IsNullOrEmpty(configurationSection))
            {
                var options = serviceProvider.GetRequiredService<IOptions<TenantStoreBlobStorageOptions<TStore>>>().Value;

                if (options.IsConfigured())
                {
                    var contentTypeProvider = serviceProvider.GetService<IContentTypeProvider>() ?? new FileExtensionContentTypeProvider();
                    var blobStore = new BlobFileStore(options, serviceProvider.GetRequiredService<IClock>(), contentTypeProvider);

                    return factory(serviceProvider, blobStore);
                }

                serviceProvider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(TenantFileStoreServiceCollectionExtensions))
                    .LogError(
                        "Azure Blob Storage for the {Purpose} file store is enabled but not active, because the 'ConnectionString' or the 'ContainerName' is missing from the '{Section}' configuration section.",
                        purpose,
                        configurationSection);
            }

            if (replaced is not null)
            {
                return (TStore)Resolve(serviceProvider, replaced);
            }

            return factory(serviceProvider, CreateLocalStore(serviceProvider, purpose));
        }));

        return services;
    }

    /// <summary>
    /// Creates the file store kept in <c>App_Data/Sites/{tenant}/{purpose}</c>.
    /// </summary>
    /// <param name="serviceProvider">The tenant's services.</param>
    /// <param name="purpose">The folder inside the tenant's application data.</param>
    /// <returns>The file store.</returns>
    public static IFileStore CreateLocalStore(IServiceProvider serviceProvider, string purpose)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        var shellOptions = serviceProvider.GetRequiredService<IOptions<ShellOptions>>().Value;
        var shellSettings = serviceProvider.GetRequiredService<ShellSettings>();
        var path = Path.Combine(shellOptions.ShellsApplicationDataPath, shellOptions.ShellsContainerName, shellSettings.Name, purpose);

        return new FileSystemStore(path, serviceProvider.GetRequiredService<ILogger<FileSystemStore>>());
    }

    private static object Resolve(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(serviceProvider);
        }

        return ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType);
    }
}
