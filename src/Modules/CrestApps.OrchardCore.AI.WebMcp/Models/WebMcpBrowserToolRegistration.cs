namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// The page tools a user's browser tab offers the AI chat.
/// </summary>
public sealed class WebMcpBrowserToolRegistration
{
    /// <summary>
    /// Gets the identifier of the user.
    /// </summary>
    public string UserId { get; init; }

    /// <summary>
    /// Gets the SignalR connection of the tab that runs the tools.
    /// </summary>
    public string ConnectionId { get; init; }

    /// <summary>
    /// Gets the tools.
    /// </summary>
    public IReadOnlyList<WebMcpBrowserTool> Tools { get; init; } = [];
}
