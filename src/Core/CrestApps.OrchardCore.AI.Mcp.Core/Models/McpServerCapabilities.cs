namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

/// <summary>
/// Represents the cached metadata for an MCP server, including all its capabilities.
/// </summary>
public sealed class McpServerCapabilities
{
    /// <summary>
    /// Gets or sets the connection identifier for this MCP server.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the connection.
    /// </summary>
    public string ConnectionDisplayText { get; set; }

    /// <summary>
    /// Gets or sets the list of tool capabilities.
    /// </summary>
    public IReadOnlyList<McpServerCapability> Tools { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of prompt capabilities.
    /// </summary>
    public IReadOnlyList<McpServerCapability> Prompts { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of resource capabilities.
    /// </summary>
    public IReadOnlyList<McpServerCapability> Resources { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of resource template capabilities.
    /// Resource templates have parameterized URIs (e.g., recipe-schema://id/recipe/{name}).
    /// </summary>
    public IReadOnlyList<McpServerCapability> ResourceTemplates { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC date and time when the capabilities were fetched.
    /// </summary>
    public DateTime FetchedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the server was reachable when capabilities were fetched.
    /// </summary>
    public bool IsHealthy { get; set; }
}
