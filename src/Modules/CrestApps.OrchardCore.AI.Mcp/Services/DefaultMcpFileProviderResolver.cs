using CrestApps.OrchardCore.AI.Mcp.Core;
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

    public DefaultMcpFileProviderResolver(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment;
    }

    public IFileProvider Resolve(string providerName)
    {
        return _webHostEnvironment.ContentRootFileProvider;
    }
}
