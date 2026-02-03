using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

/// <summary>
/// Represents an argument that can be passed to an MCP prompt.
/// </summary>
public sealed class McpPromptArgument : ICloneable<McpPromptArgument>
{
    /// <summary>
    /// Gets or sets the name of the argument.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the argument.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether this argument is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Creates a copy of this argument.
    /// </summary>
    public McpPromptArgument Clone()
    {
        return new McpPromptArgument
        {
            Name = Name,
            Description = Description,
            IsRequired = IsRequired,
        };
    }
}
