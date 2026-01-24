using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core;

public static class AIPermissions
{
    public static readonly Permission ManageAIToolInstances = new("ManageAIToolInstances", "Manage AI Tool Instances");

    public static readonly Permission ManageProviderConnections = new("ManageProviderConnections", "Manage AI Provider Connections");

    public static readonly Permission ManageAIProfiles = new("ManageAIProfiles", "Manage AI profiles");

    public static readonly Permission ManageAIDeployments = new("ManageAIDeployments", "Manage AI deployments");

    public static readonly Permission ManageAIDataSources = new("ManageAIDataSources", "Manage AI data sources");

    public static readonly Permission QueryAnyAIProfile = new("QueryAnyAIProfile", "Query any AI profile");

    public static readonly Permission DeleteChatSession = new("DeleteChatSession", "Delete chat session");

    public static readonly Permission DeleteAllChatSessions = new("DeleteAllChatSessions", "Delete all chat sessions");

    public static readonly Permission ManageChatInteractionSettings = new("ManageChatInteractionSettings", "Manage chat interaction settings");

    public static readonly Permission ListChatInteractionsForOthers = new("ListChatInteractionsForOthers", "List chat interactions for others");

    public static readonly Permission ListChatInteractions = new("ListChatInteractions", "List chat interactions", [ListChatInteractionsForOthers]);

    public static readonly Permission EditChatInteractions = new("EditChatInteractions", "Edit any chat interactions");

    public static readonly Permission EditOwnChatInteractions = new("EditOwnChatInteractions", "Edit own chat interactions", [EditChatInteractions]);

    public static readonly Permission DeleteChatInteraction = new("DeleteChatInteraction", "Delete chat interaction");

    public static readonly Permission DeleteOwnChatInteraction = new("DeleteOwnChatInteraction", "Delete own chat interaction", [DeleteChatInteraction]);

    public static readonly Permission AccessAnyAITool = new("AccessAnyAITool", "Access any AI tool", [], isSecurityCritical: true);

    public static readonly Permission AccessAITool = new("AccessAITool", "Access AI tool");

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
