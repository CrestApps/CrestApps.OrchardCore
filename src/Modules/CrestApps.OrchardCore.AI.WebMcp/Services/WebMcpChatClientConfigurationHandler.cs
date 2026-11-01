using CrestApps.OrchardCore.AI.WebMcp.Hubs;
using CrestApps.OrchardCore.AI.WebMcp.Models;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Adds the chat bridge script and the address of its hub to the pages that offer WebMCP tools.
/// </summary>
internal sealed class WebMcpChatClientConfigurationHandler : IWebMcpClientConfigurationHandler
{
    /// <summary>
    /// Adds the chat bridge script and the tenant's hub address.
    /// </summary>
    /// <param name="context">The configuration of the page being built.</param>
    /// <returns>A completed task.</returns>
    public Task ConfigureAsync(WebMcpClientConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.AddScript(WebMcpConstants.ResourceNames.ChatBridgeScript);
        context.Settings["chatHubUrl"] = SignalRHubRoutes.GetTenantAwareHubUrl<WebMcpHub>(context.HttpContext);

        return Task.CompletedTask;
    }
}
