namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// The options of the WebMCP Chat Bridge feature, bound from the <c>CrestApps:AI:WebMcp:Chat</c> shell configuration section.
/// </summary>
public sealed class WebMcpChatOptions
{
    /// <summary>
    /// Gets or sets how long the chat waits for the browser to answer a page tool call. The default is 60 seconds.
    /// </summary>
    public TimeSpan ToolCallTimeout { get; set; } = TimeSpan.FromSeconds(60);
}
