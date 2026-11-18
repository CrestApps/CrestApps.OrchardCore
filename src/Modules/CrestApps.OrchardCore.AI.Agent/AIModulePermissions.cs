using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Permissions declared in AI module assemblies this module cannot reference, mirrored by their names so the tools can
/// check them. Permissions are matched by name, so a role that holds the module's permission holds this one.
/// </summary>
internal static class AIModulePermissions
{
    /// <summary>
    /// Gets the permission to manage Agent-to-Agent connections, declared by the AI A2A module.
    /// </summary>
    public static readonly Permission ManageA2AConnections = new("ManageA2AConnections", LocalizationSource.Create("Manage Agent-to-Agent Connections", typeof(AIModulePermissions)));

    /// <summary>
    /// Gets the permission to view AI chat analytics and usage, declared by the AI Chat module.
    /// </summary>
    public static readonly Permission ViewChatAnalytics = new("ViewChatAnalytics", LocalizationSource.Create("View AI Chat Analytics", typeof(AIModulePermissions)));
}
