using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Interface for handling MCP resource events like exporting.
/// </summary>
public interface IMcpResourceHandler
{
    /// <summary>
    /// Called during resource export to allow modification of export data.
    /// </summary>
    void Exporting(ExportingMcpResourceContext context);
}

/// <summary>
/// Context provided during MCP resource export.
/// </summary>
public sealed class ExportingMcpResourceContext
{
    /// <summary>
    /// The resource being exported.
    /// </summary>
    public readonly McpResource Resource;

    /// <summary>
    /// The JSON data being exported. Can be modified to remove sensitive data.
    /// </summary>
    public readonly JsonObject ExportData;

    public ExportingMcpResourceContext(McpResource resource, JsonObject exportData)
    {
        ArgumentNullException.ThrowIfNull(resource);

        Resource = resource;
        ExportData = exportData ?? [];
    }
}
