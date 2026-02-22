using Microsoft.Extensions.FileProviders;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Resolves an <see cref="IFileProvider"/> by provider name.
/// Module developers can register additional named providers (e.g., media, web root).
/// </summary>
public interface IMcpFileProviderResolver
{
    /// <summary>
    /// Resolves an <see cref="IFileProvider"/> for the given provider name.
    /// </summary>
    /// <param name="providerName">The name of the file provider to resolve.</param>
    /// <returns>The resolved file provider, or <c>null</c> if the provider name is not recognized.</returns>
    IFileProvider Resolve(string providerName);
}
