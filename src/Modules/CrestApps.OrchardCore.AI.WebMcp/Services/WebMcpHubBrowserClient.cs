using CrestApps.OrchardCore.AI.WebMcp.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Sends requests to the chat bridge script through <see cref="WebMcpHub"/>.
/// </summary>
internal sealed class WebMcpHubBrowserClient : IWebMcpBrowserClient
{
    /// <summary>
    /// The client method the chat bridge script handles.
    /// </summary>
    public const string ExecuteToolMethod = "ExecuteTool";

    private readonly IHubContext<WebMcpHub> _hubContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMcpHubBrowserClient"/> class.
    /// </summary>
    /// <param name="hubContext">The hub context.</param>
    public WebMcpHubBrowserClient(IHubContext<WebMcpHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <inheritdoc/>
    public Task SendExecuteToolAsync(string connectionId, string callId, string toolName, string argumentsJson, CancellationToken cancellationToken)
    {
        return _hubContext.Clients.Client(connectionId).SendAsync(ExecuteToolMethod, callId, toolName, argumentsJson, cancellationToken);
    }
}
