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
    public static readonly Permission ManageProviderConnections = new("ManageProviderConnections", "Manage AI Provider Connections");

    /// <summary>
    /// Gets the permission to manage AI profiles.
    /// </summary>
    public static readonly Permission ManageAIProfiles = new("ManageAIProfiles", "Manage AI profiles");

    /// <summary>
    /// Gets the permission to manage AI profile templates.
    /// </summary>
    public static readonly Permission ManageAIProfileTemplates = new("ManageAIProfileTemplates", "Manage AI profile templates");

    /// <summary>
    /// Gets the permission to manage AI deployments.
    /// </summary>
    public static readonly Permission ManageAIDeployments = new("ManageAIDeployments", "Manage AI deployments");

    /// <summary>
    /// Gets the permission to manage AI data sources.
    /// </summary>
    public static readonly Permission ManageAIDataSources = new("ManageAIDataSources", "Manage AI data sources");

    /// <summary>
    /// Gets the permission to query any AI profile.
    /// </summary>
    public static readonly Permission QueryAnyAIProfile = new("QueryAnyAIProfile", "Query any AI profile");

    /// <summary>
    /// Gets the permission to delete a chat session.
    /// </summary>
    public static readonly Permission DeleteChatSession = new("DeleteChatSession", "Delete chat session");

    /// <summary>
    /// Gets the permission to delete all chat sessions.
    /// </summary>
    public static readonly Permission DeleteAllChatSessions = new("DeleteAllChatSessions", "Delete all chat sessions");

    /// <summary>
    /// Gets the permission to manage chat interaction settings.
    /// </summary>
    public static readonly Permission ManageChatInteractionSettings = new("ManageChatInteractionSettings", "Manage chat interaction settings");

    /// <summary>
    /// Gets the permission to list chat interactions for others.
    /// </summary>
    public static readonly Permission ListChatInteractionsForOthers = new("ListChatInteractionsForOthers", "List chat interactions for others");

    /// <summary>
    /// Gets the permission to list chat interactions.
    /// </summary>
    public static readonly Permission ListChatInteractions = new("ListChatInteractions", "List chat interactions", [ListChatInteractionsForOthers]);

    /// <summary>
    /// Gets the permission to edit any chat interactions.
    /// </summary>
    public static readonly Permission EditChatInteractions = new("EditChatInteractions", "Edit any chat interactions");

    /// <summary>
    /// Gets the permission to edit own chat interactions.
    /// </summary>
    public static readonly Permission EditOwnChatInteractions = new("EditOwnChatInteractions", "Edit own chat interactions", [EditChatInteractions]);

    /// <summary>
    /// Gets the permission to delete a chat interaction.
    /// </summary>
    public static readonly Permission DeleteChatInteraction = new("DeleteChatInteraction", "Delete chat interaction");

    /// <summary>
    /// Gets the permission to delete own chat interactions.
    /// </summary>
    public static readonly Permission DeleteOwnChatInteraction = new("DeleteOwnChatInteraction", "Delete own chat interaction", [DeleteChatInteraction]);

    /// <summary>
    /// Gets the security-critical permission to access any AI tool.
    /// </summary>
    public static readonly Permission AccessAnyAITool = new("AccessAnyAITool", "Access any AI tool", [], isSecurityCritical: true);

    /// <summary>
    /// Gets the permission to access an AI tool.
    /// </summary>
    public static readonly Permission AccessAITool = new("AccessAITool", "Access AI tool", [AccessAnyAITool]);

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
        string.Format(_queryAIProfileTemplate.Description, profileName),
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
        string.Format(_accessAIToolTemplate.Description, toolName),
        _accessAIToolTemplate.ImpliedBy ?? []
        );
    }
}
