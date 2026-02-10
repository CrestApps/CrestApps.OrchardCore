using System.Text.Json;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

/// <summary>
/// Represents a single capability (tool, prompt, or resource) exposed by an MCP server.
/// </summary>
public sealed class McpServerCapability
{
    /// <summary>
    /// Gets or sets the unique name/identifier of this capability within the server.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of what this capability does.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the JSON schema describing the input parameters for this capability.
    /// Applicable to tools and prompts.
    /// </summary>
    public JsonElement? InputSchema { get; set; }

    /// <summary>
    /// Gets or sets the MIME type of the resource.
    /// Only applicable when this capability is a resource.
    /// </summary>
    public string MimeType { get; set; }

    /// <summary>
    /// Gets or sets the URI for resources.
    /// Only applicable when this capability is a resource.
    /// </summary>
    public string Uri { get; set; }
}
