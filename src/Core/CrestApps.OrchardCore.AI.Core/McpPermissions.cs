using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Represents the mcp permissions.
/// </summary>
public static class McpPermissions
{
    public static readonly Permission ManageMcpConnections = new("ManageMcpConnections", LocalizationSource.Create("Manage MCP Connections", typeof(McpPermissions)));

    /// <summary>
    /// Creating or changing a local (stdio) connection chooses a process the server starts, so it is its own permission,
    /// marked security-critical, and only ever granted on the default tenant.
    /// </summary>
    public static readonly Permission ManageMcpStdioConnections = new("ManageMcpStdioConnections", LocalizationSource.Create("Manage local (stdio) MCP connections, which start processes on the server", typeof(McpPermissions)), isSecurityCritical: true);

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
