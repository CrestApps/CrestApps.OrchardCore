using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Defines the WebMCP scripts.
/// </summary>
internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript(WebMcpConstants.ResourceNames.Script)
            .SetUrl("~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp.min.js", "~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp.js")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript(WebMcpConstants.ResourceNames.PageToolsScript)
            .SetUrl("~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp-page-tools.min.js", "~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp-page-tools.js")
            .SetDependencies(WebMcpConstants.ResourceNames.Script)
            .SetVersion("1.0.0");

        _manifest
            .DefineScript(WebMcpConstants.ResourceNames.ChatBridgeScript)
            .SetUrl("~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp-chat-bridge.min.js", "~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp-chat-bridge.js")
            .SetDependencies(WebMcpConstants.ResourceNames.Script, "signalr")
            .SetVersion("1.0.0");
    }

    /// <summary>
    /// Adds the WebMCP scripts to the resource manifests.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
