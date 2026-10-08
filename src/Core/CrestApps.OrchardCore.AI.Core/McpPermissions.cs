using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Represents the mcp permissions.
/// </summary>
public static class McpPermissions
{
    public static readonly Permission ManageMcpConnections = new("ManageMcpConnections", LocalizationSource.Create("Manage MCP Connections", typeof(McpPermissions)));

    public static readonly Permission ManageMcpPrompts = new("ManageMcpPrompts", LocalizationSource.Create("Manage MCP Prompts", typeof(McpPermissions)));

    public static readonly Permission ManageMcpResources = new("ManageMcpResources", LocalizationSource.Create("Manage MCP Resources", typeof(McpPermissions)));

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
