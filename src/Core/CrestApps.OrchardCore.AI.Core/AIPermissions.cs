using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Represents the AI permissions.
/// </summary>
public static class AIPermissions
{
    /// <summary>
    /// Gets the permission to manage AI provider connections.
    /// </summary>
    public static readonly Permission ManageProviderConnections = new("ManageProviderConnections", LocalizationSource.Create("Manage AI Provider Connections", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to manage AI profiles.
    /// </summary>
    public static readonly Permission ManageAIProfiles = new("ManageAIProfiles", LocalizationSource.Create("Manage AI profiles", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to manage AI profile templates.
    /// </summary>
    public static readonly Permission ManageAIProfileTemplates = new("ManageAIProfileTemplates", LocalizationSource.Create("Manage AI profile templates", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to manage AI deployments.
    /// </summary>
    public static readonly Permission ManageAIDeployments = new("ManageAIDeployments", LocalizationSource.Create("Manage AI deployments", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to manage AI tool instances that were created by other users.
    /// </summary>
    public static readonly Permission ManageAIToolInstancesCreatedByOthers = new("ManageAIToolInstancesCreatedByOthers", LocalizationSource.Create("Manage AI tool instances created by others", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to manage AI tool instances. This permission is implied by
    /// <see cref="ManageAIToolInstancesCreatedByOthers"/>, so callers only ever need to check this one to
    /// determine whether the user may reach the tool instance management surface.
    /// </summary>
    public static readonly Permission ManageAIToolInstances = new("ManageAIToolInstances", LocalizationSource.Create("Manage AI tool instances", typeof(AIPermissions)), [ManageAIToolInstancesCreatedByOthers]);

    /// <summary>
    /// Gets the permission to manage AI data sources.
    /// </summary>
    public static readonly Permission ManageAIDataSources = new("ManageAIDataSources", LocalizationSource.Create("Manage AI data sources", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to query any AI profile.
    /// </summary>
    public static readonly Permission QueryAnyAIProfile = new("QueryAnyAIProfile", LocalizationSource.Create("Query any AI profile", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to delete a chat session.
    /// </summary>
    public static readonly Permission DeleteChatSession = new("DeleteChatSession", LocalizationSource.Create("Delete chat session", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to delete all chat sessions.
    /// </summary>
    public static readonly Permission DeleteAllChatSessions = new("DeleteAllChatSessions", LocalizationSource.Create("Delete all chat sessions", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to manage chat interaction settings.
    /// </summary>
    public static readonly Permission ManageChatInteractionSettings = new("ManageChatInteractionSettings", LocalizationSource.Create("Manage chat interaction settings", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to list chat interactions for others.
    /// </summary>
    public static readonly Permission ListChatInteractionsForOthers = new("ListChatInteractionsForOthers", LocalizationSource.Create("List chat interactions for others", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to list chat interactions.
    /// </summary>
    public static readonly Permission ListChatInteractions = new("ListChatInteractions", LocalizationSource.Create("List chat interactions", typeof(AIPermissions)), [ListChatInteractionsForOthers]);

    /// <summary>
    /// Gets the permission to edit any chat interactions.
    /// </summary>
    public static readonly Permission EditChatInteractions = new("EditChatInteractions", LocalizationSource.Create("Edit any chat interactions", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to edit own chat interactions.
    /// </summary>
    public static readonly Permission EditOwnChatInteractions = new("EditOwnChatInteractions", LocalizationSource.Create("Edit own chat interactions", typeof(AIPermissions)), [EditChatInteractions]);

    /// <summary>
    /// Gets the permission to delete a chat interaction.
    /// </summary>
    public static readonly Permission DeleteChatInteraction = new("DeleteChatInteraction", LocalizationSource.Create("Delete chat interaction", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to delete own chat interactions.
    /// </summary>
    public static readonly Permission DeleteOwnChatInteraction = new("DeleteOwnChatInteraction", LocalizationSource.Create("Delete own chat interaction", typeof(AIPermissions)), [DeleteChatInteraction]);

    /// <summary>
    /// Gets the permission to clear AI memory for any user.
    /// </summary>
    public static readonly Permission ClearAIMemoryForOthers = new("ClearAIMemoryForOthers", LocalizationSource.Create("Clear AI memory for other users", typeof(AIPermissions)));

    /// <summary>
    /// Gets the permission to clear AI memory.
    /// </summary>
    public static readonly Permission ClearAIMemory = new("ClearAIMemory", LocalizationSource.Create("Clear AI memory", typeof(AIPermissions)), [ClearAIMemoryForOthers]);

    /// <summary>
    /// Gets the security-critical permission to access any AI tool.
    /// </summary>
    public static readonly Permission AccessAnyAITool = new("AccessAnyAITool", LocalizationSource.Create("Access any AI tool", typeof(AIPermissions)), [], isSecurityCritical: true);

    /// <summary>
    /// Gets the permission to access an AI tool.
    /// </summary>
    public static readonly Permission AccessAITool = new("AccessAITool", LocalizationSource.Create("Access AI tool", typeof(AIPermissions)), [AccessAnyAITool]);

    private static readonly Permission _queryAIProfileTemplate = new("QueryAIProfile_{0}", "Query AI profile - {0}", [QueryAnyAIProfile]);

    private static readonly Permission _accessAIToolTemplate = new("AccessAITool_{0}", "Access AI tool - {0}", [AccessAnyAITool]);

    /// <summary>
    /// Generates a permission dynamically for a content type.
    /// </summary>
    public static Permission CreateProfilePermission(string profileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(profileName);

        return new Permission(
            string.Format(_queryAIProfileTemplate.Name, profileName),
        string.Format(_queryAIProfileTemplate.Description?.Value, profileName),
        _queryAIProfileTemplate.ImpliedBy ?? []
        );
    }

    /// <summary>
    /// Generates a permission dynamically for an AI tool.
    /// </summary>
    public static Permission CreateAIToolPermission(string toolName)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);

        return new Permission(
            string.Format(_accessAIToolTemplate.Name, toolName),
        string.Format(_accessAIToolTemplate.Description?.Value, toolName),
        _accessAIToolTemplate.ImpliedBy ?? []
        );
    }
}
