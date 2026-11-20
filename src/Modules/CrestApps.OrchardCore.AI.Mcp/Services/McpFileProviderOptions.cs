using Microsoft.Extensions.FileProviders;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// The named file providers a <c>file</c> MCP resource may read from, in addition to the tenant's own
/// MCP files folder.
/// </summary>
/// <remarks>
/// Everything a provider can reach is readable by the MCP clients allowed to read resources, so register only
/// providers rooted at folders that hold files meant for those clients. Never register the content root or the
/// application data folder: they hold configuration, connection strings, keys and every tenant's database.
/// </remarks>
public sealed class McpFileProviderOptions
{
    private readonly Dictionary<string, Func<IServiceProvider, IFileProvider>> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered providers, keyed by the name a resource puts in its <c>{providerName}</c> variable.
    /// </summary>
    public IReadOnlyDictionary<string, Func<IServiceProvider, IFileProvider>> Providers => _providers;

    /// <summary>
    /// Registers a named file provider.
    /// </summary>
    /// <param name="name">The name a resource uses to select the provider.</param>
    /// <param name="factory">Creates the provider in the request's service scope.</param>
    /// <returns>The options, for chaining.</returns>
    public McpFileProviderOptions AddProvider(string name, Func<IServiceProvider, IFileProvider> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);

        _providers[name] = factory;

        return this;
    }
}
