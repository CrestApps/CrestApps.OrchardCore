using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a tool entry in the unified tool registry.
/// Contains searchable metadata used for tool scoping during orchestration.
/// </summary>
public sealed class ToolRegistryEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for this entry.
    /// Used for internal retrieval and to avoid name collisions across sources.
    /// For local/system tools this equals <see cref="Name"/>;
    /// for MCP tools it includes the source identifier (e.g., <c>mcp:{connectionId}:{toolName}</c>).
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the function name of the tool as presented to the AI model.
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

    /// <summary>
    /// Gets or sets a factory delegate that creates the actual <see cref="AITool"/> for this entry.
    /// Each provider sets this to its own resolution logic (e.g., DI lookup for local tools,
    /// MCP proxy creation for MCP tools).
    /// </summary>
    public Func<IServiceProvider, ValueTask<AITool>> ToolFactory { get; set; }
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
