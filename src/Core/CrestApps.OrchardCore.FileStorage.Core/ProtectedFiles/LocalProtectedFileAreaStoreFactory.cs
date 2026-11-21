using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.FileStorage;
using OrchardCore.FileStorage.FileSystem;

namespace CrestApps.OrchardCore.FileStorage.ProtectedFiles;

/// <summary>
/// Keeps every protected file area in a folder of the tenant's application data:
/// <c>App_Data/Sites/{tenant}/{folder}</c>.
/// </summary>
public sealed class LocalProtectedFileAreaStoreFactory : IProtectedFileAreaStoreFactory
{
    private readonly ConcurrentDictionary<string, IFileStore> _stores = new(StringComparer.Ordinal);
    private readonly string _tenantPath;
    private readonly ILogger<FileSystemStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalProtectedFileAreaStoreFactory"/> class.
    /// </summary>
    /// <param name="shellOptions">The shell options.</param>
    /// <param name="shellSettings">The tenant's settings.</param>
    /// <param name="logger">The logger of the file system stores.</param>
    public LocalProtectedFileAreaStoreFactory(
        IOptions<ShellOptions> shellOptions,
        ShellSettings shellSettings,
        ILogger<FileSystemStore> logger)
    {
        _tenantPath = Path.Combine(shellOptions.Value.ShellsApplicationDataPath, shellOptions.Value.ShellsContainerName, shellSettings.Name);
        _logger = logger;
    }

    /// <inheritdoc/>
    public IFileStore GetStore(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);

        return _stores.GetOrAdd(folder, name => new FileSystemStore(Path.Combine(_tenantPath, name), _logger));
    }
}
