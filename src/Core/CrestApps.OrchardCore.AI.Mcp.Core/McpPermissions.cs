using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

public static class McpPermissions
{
    public static readonly Permission ManageMcpConnections = new("ManageMcpConnections", "Manage MCP Connections");

    public static readonly Permission ManageMcpPrompts = new("ManageMcpPrompts", "Manage MCP Prompts");

    public static readonly Permission ManageMcpResources = new("ManageMcpResources", "Manage MCP Resources");
}
