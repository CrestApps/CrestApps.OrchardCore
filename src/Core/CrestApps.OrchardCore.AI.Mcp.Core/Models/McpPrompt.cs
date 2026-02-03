using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

/// <summary>
/// Represents an MCP prompt that can be exposed via the MCP server.
/// </summary>
public sealed class McpPrompt : CatalogItem, IDisplayTextAwareModel, ICloneable<McpPrompt>
{
    /// <summary>
    /// Gets or sets the display text (title) for the prompt.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the unique name for the prompt used by MCP clients.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of what the prompt does.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the prompt was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the name of the author who created the prompt.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who owns this prompt.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the list of arguments that can be passed to this prompt.
    /// </summary>
    public List<McpPromptArgument> Arguments { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of messages that make up this prompt.
    /// </summary>
    public List<McpPromptMessage> Messages { get; set; } = [];

    /// <summary>
    /// Creates a deep copy of this prompt.
    /// </summary>
    public McpPrompt Clone()
    {
        return new McpPrompt()
        {
            ItemId = ItemId,
            DisplayText = DisplayText,
            Name = Name,
            Description = Description,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties,
            Arguments = Arguments?.Select(a => a.Clone()).ToList() ?? [],
            Messages = Messages?.Select(m => m.Clone()).ToList() ?? [],
        };
    }
}
