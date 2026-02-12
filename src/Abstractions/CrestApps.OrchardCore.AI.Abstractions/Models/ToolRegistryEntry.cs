namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a tool entry in the unified tool registry.
/// Contains searchable metadata used for tool scoping during orchestration.
/// </summary>
public sealed class ToolRegistryEntry
{
    /// <summary>
    /// Gets or sets the unique name of the tool.
    /// For local tools, this is the registered tool name.
    /// For MCP tools, this is the tool name as reported by the MCP server.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of the tool's capabilities.
    /// Used for relevance matching during tool scoping.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the source type of this tool entry.
    /// </summary>
    public ToolRegistryEntrySource Source { get; set; }

    /// <summary>
    /// Gets or sets the source identifier.
    /// For MCP tools, this is the MCP connection ID.
    /// For local tools, this is <see langword="null"/>.
    /// </summary>
    public string SourceId { get; set; }
}

/// <summary>
/// Identifies the origin of a tool registry entry.
/// </summary>
public enum ToolRegistryEntrySource
{
    /// <summary>
    /// A locally registered AI tool.
    /// </summary>
    Local,

    /// <summary>
    /// A tool provided by an MCP server connection.
    /// </summary>
    McpServer,

    /// <summary>
    /// A system tool automatically included by the orchestrator based on context availability.
    /// </summary>
    System,
}
