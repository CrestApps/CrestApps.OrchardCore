using CrestApps.Core.AI.Mcp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Default file provider resolver that returns the web host's content root file provider.
/// Additional providers (e.g., media) can be registered by other modules.
/// </summary>
public sealed class DefaultMcpFileProviderResolver : IMcpFileProviderResolver
{
    private readonly IWebHostEnvironment _webHostEnvironment;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMcpFileProviderResolver"/> class.
    /// </summary>
    /// <param name="webHostEnvironment">The web host environment.</param>
    public DefaultMcpFileProviderResolver(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment;
    }

    /// <summary>
    /// Performs the resolve operation.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    public IFileProvider Resolve(string providerName)
    {
        return _webHostEnvironment.ContentRootFileProvider;
    }
}
