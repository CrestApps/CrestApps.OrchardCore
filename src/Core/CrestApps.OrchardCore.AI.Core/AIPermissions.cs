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

    private static readonly Permission _queryAIProfileTemplate = new("QueryAIProfile_{0}", "Query AI profile - {0}", [QueryAnyAIProfile]);

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
}
