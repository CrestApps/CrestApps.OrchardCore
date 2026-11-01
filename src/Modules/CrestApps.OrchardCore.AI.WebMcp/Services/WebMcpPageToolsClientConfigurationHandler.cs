using CrestApps.OrchardCore.AI.WebMcp.Models;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Adds the page tools script to the pages that offer WebMCP tools.
/// </summary>
internal sealed class WebMcpPageToolsClientConfigurationHandler : IWebMcpClientConfigurationHandler
{
    /// <summary>
    /// Adds the page tools script.
    /// </summary>
    /// <param name="context">The configuration of the page being built.</param>
    /// <returns>A completed task.</returns>
    public Task ConfigureAsync(WebMcpClientConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.AddScript(WebMcpConstants.ResourceNames.PageToolsScript);

        return Task.CompletedTask;
    }
}
