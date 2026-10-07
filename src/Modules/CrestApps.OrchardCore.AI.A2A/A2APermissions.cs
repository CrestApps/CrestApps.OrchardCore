using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.A2A;

/// <summary>
/// Represents the a2 a permissions.
/// </summary>
public static class A2APermissions
{
    public static readonly Permission ManageA2AConnections = new("ManageA2AConnections", LocalizationSource.Create("Manage Agent-to-Agent Connections", typeof(A2APermissions)));

    public static readonly Permission AccessA2AHost = new("AccessA2AHost", LocalizationSource.Create("Access the A2A Host", typeof(A2APermissions)), isSecurityCritical: true);
}
