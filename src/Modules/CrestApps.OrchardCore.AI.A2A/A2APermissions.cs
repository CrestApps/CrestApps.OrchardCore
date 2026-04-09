using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.A2A;

public static class A2APermissions
{
    public static readonly Permission ManageA2AConnections = new("ManageA2AConnections", "Manage Agent-to-Agent Connections");
}
