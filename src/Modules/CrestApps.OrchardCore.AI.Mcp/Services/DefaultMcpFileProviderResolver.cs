using CrestApps.Core.AI.Mcp;
using CrestApps.OrchardCore.Core.FileStorage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Resolves the file provider a <c>file</c> MCP resource reads from.
/// </summary>
/// <remarks>
/// <para>
/// Without a provider name, files come from the tenant's own <c>McpFiles</c> folder
/// (<c>App_Data/Sites/{tenant}/McpFiles</c>); while that folder does not exist, no file is found. A path that leaves the
/// folder, by <c>..</c>, an absolute path or a link that points outside it, and a dot-prefixed file or folder, are not
/// found. A provider name
/// selects a provider registered in <see cref="McpFileProviderOptions"/>; an unknown name resolves to nothing.
/// </para>
/// <para>
/// The content root is never used: it holds <c>appsettings.json</c> and the whole <c>App_Data</c> folder, with every
/// tenant's database, settings and keys, and an MCP client chooses the file name it reads.
/// </para>
/// </remarks>
public sealed class DefaultMcpFileProviderResolver : IMcpFileProviderResolver
{
    /// <summary>
    /// The name of the tenant folder that holds the files of <c>file</c> resources.
    /// </summary>
    public const string TenantFolderName = "McpFiles";

    private readonly ShellSettings _shellSettings;
    private readonly ShellOptions _shellOptions;
    private readonly McpFileProviderOptions _options;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMcpFileProviderResolver"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings, which name the tenant.</param>
    /// <param name="shellOptions">The shell options, which locate the tenant's application data folder.</param>
    /// <param name="options">The named file providers.</param>
    /// <param name="serviceProvider">The service provider passed to the named provider factories.</param>
    public DefaultMcpFileProviderResolver(
        ShellSettings shellSettings,
        IOptions<ShellOptions> shellOptions,
        IOptions<McpFileProviderOptions> options,
        IServiceProvider serviceProvider)
    {
        _shellSettings = shellSettings;
        _shellOptions = shellOptions.Value;
        _options = options.Value;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Resolves the file provider for a resource.
    /// </summary>
    /// <param name="providerName">The provider name from the resource's <c>{providerName}</c> variable, if any.</param>
    /// <returns>The provider, or <see langword="null"/> when no provider has that name.</returns>
    public IFileProvider Resolve(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            // The tenant folder's own rule refuses an absolute path, a path that climbs out with .. and a link that
            // leaves the folder, which PhysicalFileProvider would follow; dot-prefixed names are refused on top.
            return new NoDotFilesProvider(new TenantFolderFileProvider(new TenantFolder(_shellSettings, _shellOptions, TenantFolderName)));
        }

        return _options.Providers.TryGetValue(providerName.Trim(), out var factory)
            ? factory(_serviceProvider)
            : null;
    }

    /// <summary>
    /// Gets the full path of the tenant's MCP files folder.
    /// </summary>
    public string GetTenantFolder()
        => Path.GetFullPath(Path.Combine(
            _shellOptions.ShellsApplicationDataPath,
            _shellOptions.ShellsContainerName,
            _shellSettings.Name,
            TenantFolderName));

    private sealed class NoDotFilesProvider : IFileProvider
    {
        private readonly IFileProvider _inner;

        public NoDotFilesProvider(IFileProvider inner)
        {
            _inner = inner;
        }

        public IFileInfo GetFileInfo(string subpath)
            => HasDotSegment(subpath) ? new NotFoundFileInfo(subpath ?? string.Empty) : _inner.GetFileInfo(subpath);

        public IDirectoryContents GetDirectoryContents(string subpath)
            => HasDotSegment(subpath) ? NotFoundDirectoryContents.Singleton : _inner.GetDirectoryContents(subpath);

        public IChangeToken Watch(string filter)
            => NullChangeToken.Singleton;

        // A "." or ".." segment is left to the tenant folder's rule; any other segment that starts with a dot is hidden.
        private static bool HasDotSegment(string subpath)
            => !string.IsNullOrEmpty(subpath) &&
                subpath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                    .Any(segment => segment.Length > 1 && segment[0] == '.' && segment != "..");
    }
}
