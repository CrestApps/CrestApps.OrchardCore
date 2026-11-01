namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Sends requests to the chat bridge script in a user's browser tab.
/// </summary>
public interface IWebMcpBrowserClient
{
    /// <summary>
    /// Asks a tab to run one of its page tools.
    /// </summary>
    /// <param name="connectionId">The SignalR connection of the tab.</param>
    /// <param name="callId">The identifier the tab answers the call with.</param>
    /// <param name="toolName">The function name of the tool.</param>
    /// <param name="argumentsJson">The tool's arguments, as a JSON object.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the request is sent.</returns>
    Task SendExecuteToolAsync(string connectionId, string callId, string toolName, string argumentsJson, CancellationToken cancellationToken);
}
