using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Represents the mcp permissions.
/// </summary>
public static class McpPermissions
{
    public static readonly Permission ManageMcpConnections = new("ManageMcpConnections", "Manage MCP Connections");

    public static readonly Permission ManageMcpPrompts = new("ManageMcpPrompts", "Manage MCP Prompts");

    public static readonly Permission ManageMcpResources = new("ManageMcpResources", "Manage MCP Resources");

    /// <summary>
    /// Represents the feature.
    /// </summary>
    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.AI.Mcp";

        public const string Stdio = "CrestApps.OrchardCore.AI.Mcp.Stdio";

        public const string Server = "CrestApps.OrchardCore.AI.Mcp.Server";
    }
}
