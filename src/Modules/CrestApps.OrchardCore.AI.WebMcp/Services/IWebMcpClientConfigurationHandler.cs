using CrestApps.OrchardCore.AI.WebMcp.Models;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Adds scripts and settings to the WebMCP configuration of a page. The WebMCP script filter calls every registered
/// handler on each page that offers WebMCP tools.
/// </summary>
public interface IWebMcpClientConfigurationHandler
{
    /// <summary>
    /// Adds scripts and settings to the page's WebMCP configuration.
    /// </summary>
    /// <param name="context">The configuration of the page being built.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task ConfigureAsync(WebMcpClientConfigurationContext context);
}
