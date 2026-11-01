using CrestApps.OrchardCore.AI.WebMcp.Models;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Runs page tools in a user's browser tab for the AI chat and waits for the answers.
/// </summary>
public interface IWebMcpBrowserToolInvoker
{
    /// <summary>
    /// Asks a tab to run a page tool and waits for its answer.
    /// </summary>
    /// <param name="connectionId">The SignalR connection of the tab.</param>
    /// <param name="toolName">The function name of the tool.</param>
    /// <param name="argumentsJson">The tool's arguments, as a JSON object.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// What the tab answered; or a failure when the tab cannot be reached, closes, or does not answer within
    /// <see cref="WebMcpChatOptions.ToolCallTimeout"/>.
    /// </returns>
    Task<WebMcpBrowserToolResult> InvokeAsync(string connectionId, string toolName, string argumentsJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a tab's answer to a call.
    /// </summary>
    /// <param name="callId">The identifier of the call.</param>
    /// <param name="connectionId">The SignalR connection that answers, which must be the one the call was sent to.</param>
    /// <param name="text">The text the tool answered.</param>
    /// <param name="isError">Whether the answer reports a failure.</param>
    /// <returns><see langword="true"/> when the answer completed a waiting call; otherwise <see langword="false"/>.</returns>
    bool TryComplete(string callId, string connectionId, string text, bool isError);

    /// <summary>
    /// Ends, with a failure, every call waiting on a closed connection.
    /// </summary>
    /// <param name="connectionId">The SignalR connection.</param>
    void CancelConnection(string connectionId);
}
