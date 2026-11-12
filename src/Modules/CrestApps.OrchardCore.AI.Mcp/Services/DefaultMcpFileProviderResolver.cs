using CrestApps.Core.AI.Mcp;
using CrestApps.OrchardCore.Core.FileStorage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Resolves the files an MCP "File" resource may read: the tenant's own folder,
/// <c>App_Data/Sites/{TenantName}/mcp-files</c>, and nothing else. It used to return the application's content root,
/// which let a tenant publish a resource that served the host's configuration or another tenant's data.
/// </summary>
public sealed class DefaultMcpFileProviderResolver : IMcpFileProviderResolver
{
    /// <summary>
    /// The name of the folder, inside the tenant's App_Data folder, that MCP file resources read from.
    /// </summary>
    public const string TenantFolderName = "mcp-files";

    private readonly TenantFolder _folder;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMcpFileProviderResolver"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings, which name the tenant.</param>
    /// <param name="shellOptions">The shell options, which locate the tenants' App_Data folder.</param>
    public DefaultMcpFileProviderResolver(ShellSettings shellSettings, IOptions<ShellOptions> shellOptions)
    {
        _folder = new TenantFolder(shellSettings, shellOptions.Value, TenantFolderName);
    }

    /// <summary>
    /// Returns the provider over the tenant's MCP files folder. The provider name is not used: every name reads the
    /// tenant's folder, so no name reaches another one.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    public IFileProvider Resolve(string providerName)
        => new TenantFolderFileProvider(_folder);
}
