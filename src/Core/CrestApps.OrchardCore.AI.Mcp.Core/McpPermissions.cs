using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

public static class McpPermissions
{
    public static readonly Permission ManageMcpConnections = new("ManageMcpConnections", "Manage MCP Connections");
}
