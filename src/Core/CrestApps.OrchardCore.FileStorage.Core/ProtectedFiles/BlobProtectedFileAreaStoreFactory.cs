using System.Collections.Concurrent;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.FileStorage;
using OrchardCore.FileStorage.AzureBlob;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.FileStorage.ProtectedFiles;

/// <summary>
/// Keeps every protected file area in one Azure Blob Storage container, each area under a folder of its own below the
/// configured base path.
/// </summary>
/// <remarks>
/// When the connection string or the container name is missing, the areas stay in the tenant's application data and an
/// error is logged once.
/// </remarks>
public sealed class BlobProtectedFileAreaStoreFactory : IProtectedFileAreaStoreFactory
{
    private readonly ConcurrentDictionary<string, IFileStore> _stores = new(StringComparer.Ordinal);
    private readonly TenantStoreBlobStorageOptions<ProtectedFileAreas> _options;
    private readonly LocalProtectedFileAreaStoreFactory _localStores;
    private readonly IClock _clock;
    private readonly IContentTypeProvider _contentTypeProvider;
    private readonly ILogger _logger;
    private readonly TenantFileStoreRegistration<ProtectedFileAreas> _registration;

    private int _missingLogged;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobProtectedFileAreaStoreFactory"/> class.
    /// </summary>
    /// <param name="options">The Azure Blob Storage options of the protected files.</param>
    /// <param name="registration">Where the options are read from.</param>
    /// <param name="localStores">The local stores, used while the options are incomplete.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="contentTypeProvider">The content type provider, when the tenant registers one.</param>
    public BlobProtectedFileAreaStoreFactory(
        IOptions<TenantStoreBlobStorageOptions<ProtectedFileAreas>> options,
        TenantFileStoreRegistration<ProtectedFileAreas> registration,
        LocalProtectedFileAreaStoreFactory localStores,
        IClock clock,
        ILogger<BlobProtectedFileAreaStoreFactory> logger,
        IContentTypeProvider contentTypeProvider = null)
    {
        _options = options.Value;
        _registration = registration;
        _localStores = localStores;
        _clock = clock;
        _logger = logger;
        _contentTypeProvider = contentTypeProvider ?? new FileExtensionContentTypeProvider();
    }

    /// <inheritdoc/>
    public IFileStore GetStore(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);

        if (!_options.IsConfigured())
        {
            if (Interlocked.Exchange(ref _missingLogged, 1) == 0)
            {
                _logger.LogError(
                    "Azure Blob Storage for protected files is enabled but not active, because the 'ConnectionString' or the 'ContainerName' is missing from the '{Section}' configuration section. Protected files stay in the tenant's App_Data folder.",
                    _registration.ConfigurationSection);
            }

            return _localStores.GetStore(folder);
        }

        return _stores.GetOrAdd(folder, name => new BlobFileStore(
            new TenantStoreBlobStorageOptions<ProtectedFileAreas>
            {
                ConnectionString = _options.ConnectionString,
                ContainerName = _options.ContainerName,
                BasePath = CombineBasePath(_options.BasePath, name),
                UseHierarchicalNamespace = _options.UseHierarchicalNamespace,
            },
            _clock,
            _contentTypeProvider));
    }

    /// <summary>
    /// Puts an area's folder below the configured base path.
    /// </summary>
    /// <param name="basePath">The configured base path, possibly empty.</param>
    /// <param name="folder">The folder of the area.</param>
    /// <returns>The base path of the area.</returns>
    internal static string CombineBasePath(string basePath, string folder)
    {
        var trimmed = basePath?.Trim().Trim('/', '\\');

        return string.IsNullOrEmpty(trimmed) ? folder : $"{trimmed.Replace('\\', '/')}/{folder}";
    }
}
