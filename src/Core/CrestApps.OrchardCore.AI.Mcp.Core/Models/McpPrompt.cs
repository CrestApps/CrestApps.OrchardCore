using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

/// <summary>
/// Represents an MCP prompt entry that wraps the SDK's Prompt class and adds catalog metadata.
/// </summary>
public sealed class McpPrompt : CatalogItem, IDisplayTextAwareModel, ICloneable<McpPrompt>
{
    /// <summary>
    /// Gets or sets the display text (title) for the prompt in the admin UI.
    /// </summary>
    public string DisplayText { get; set; }

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
    /// Gets or sets the MCP SDK Prompt instance containing the prompt definition.
    /// </summary>
    public Prompt Prompt { get; set; }

    /// <summary>
    /// Creates a deep copy of this prompt entry.
    /// </summary>
    public McpPrompt Clone()
    {
        var clone = new McpPrompt()
        {
            ItemId = ItemId,
            DisplayText = DisplayText,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties,
        };

        if (Prompt != null)
        {
            clone.Prompt = new Prompt
            {
                Name = Prompt.Name ?? string.Empty,
                Title = Prompt.Title,
                Description = Prompt.Description,
                Arguments = Prompt.Arguments?.Select(a => new PromptArgument
                {
                    Name = a.Name ?? string.Empty,
                    Title = a.Title,
                    Description = a.Description,
                    Required = a.Required,
                }).ToList(),
            };
        }

        return clone;
    }
}
