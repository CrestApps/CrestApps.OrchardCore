using CrestApps.OrchardCore.AI.Mcp.Core.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Generates a structured system prompt describing MCP server capabilities.
/// The generated prompt is injected into the model context so the AI can reason
/// about when to invoke MCP capabilities via the unified mcp-invoke tool.
/// </summary>
public interface IMcpMetadataPromptGenerator
{
    /// <summary>
    /// Generates a system prompt describing the given MCP server capabilities.
    /// </summary>
    /// <param name="capabilities">The capabilities of all active MCP connections.</param>
    /// <returns>
    /// A structured text prompt suitable for inclusion in the system message,
    /// or <c>null</c> if there are no capabilities to describe.
    /// </returns>
    string Generate(IReadOnlyList<McpServerCapabilities> capabilities);
}
