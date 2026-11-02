using CrestApps.OrchardCore.AI.WebMcp.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Adds the page tools script to the pages that get the WebMCP scripts, when the settings turn page tools on.
/// </summary>
internal sealed class WebMcpPageToolsClientConfigurationHandler : IWebMcpClientConfigurationHandler
{
    private readonly WebMcpPageOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMcpPageToolsClientConfigurationHandler"/> class.
    /// </summary>
    /// <param name="options">The pages and tools the settings turn on.</param>
    public WebMcpPageToolsClientConfigurationHandler(IOptionsSnapshot<WebMcpPageOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Adds the page tools script when page tools are on.
    /// </summary>
    /// <param name="context">The configuration of the page being built.</param>
    /// <returns>A completed task.</returns>
    public Task ConfigureAsync(WebMcpClientConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_options.EnablePageTools)
        {
            context.AddScript(WebMcpConstants.ResourceNames.PageToolsScript);
        }

        return Task.CompletedTask;
    }
}
