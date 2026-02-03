using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

/// <summary>
/// Represents a message within an MCP prompt.
/// </summary>
public sealed class McpPromptMessage : ICloneable<McpPromptMessage>
{
    /// <summary>
    /// Gets or sets the role of this message (e.g., "user", "assistant").
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    /// Gets or sets the content of the message.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Creates a copy of this message.
    /// </summary>
    public McpPromptMessage Clone()
    {
        return new McpPromptMessage
        {
            Role = Role,
            Content = Content,
        };
    }
}
