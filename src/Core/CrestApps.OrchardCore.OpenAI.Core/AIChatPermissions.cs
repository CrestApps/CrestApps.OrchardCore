using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public static class AIChatPermissions
{
    public static readonly Permission ManageAIChatProfiles = new("ManageAIChatProfiles", "Manage AI chat profiles");

    public static readonly Permission ManageModelDeployments = new("ManageModelDeployments", "Manage model deployments");

    public static readonly Permission QueryAnyAIChatProfile = new("QueryAnyAIChatProfile", "Query any AI chat profile");

    private static readonly Permission _queryAIChatProfileTemplate = new("QueryAIChatProfiles_{0}", "Query AI chat profile - {0}", [QueryAnyAIChatProfile]);

    /// <summary>
    /// Generates a permission dynamically for a content type.
    /// </summary>
    public static Permission CreateDynamicPermission(string profileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(profileName);

        return new Permission(
            string.Format(_queryAIChatProfileTemplate.Name, profileName),
            string.Format(_queryAIChatProfileTemplate.Description, profileName),
           _queryAIChatProfileTemplate.ImpliedBy ?? []
        );
    }
}
